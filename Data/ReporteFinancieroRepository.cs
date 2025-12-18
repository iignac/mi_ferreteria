using System;
using System.Collections.Generic;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public class ReporteFinancieroRepository : IReporteFinancieroRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ReporteFinancieroRepository> _logger;

        public ReporteFinancieroRepository(IConfiguration configuration, ILogger<ReporteFinancieroRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        private static void SetSearchPath(NpgsqlConnection conn)
        {
            using var set = new NpgsqlCommand("SET search_path TO venta, public", conn);
            set.ExecuteNonQuery();
        }

        public ReporteFinancieroResumen ObtenerResumen(int? diasTopProductos = null, int? diasTopClientes = null, long? categoriaTopProductosId = null)
        {
            var result = new ReporteFinancieroResumen();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                SetSearchPath(conn);

                CargarTotalesYMargen(conn, result);
                CargarTopProductos(conn, result, diasTopProductos, categoriaTopProductosId);
                CargarTopClientes(conn, result, diasTopClientes);
                CargarDeudores(conn, result);
                result.PromedioCobroDias = CalcularPromedioCobroDias(conn);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen financiero");
                throw;
            }
        }

        private void CargarTotalesYMargen(NpgsqlConnection conn, ReporteFinancieroResumen result)
        {
            using (var cmd = new NpgsqlCommand(@"
                SELECT
                    COALESCE(SUM(total),0) AS total,
                    COALESCE(SUM(CASE WHEN fecha >= date_trunc('day', now()) THEN total END),0) AS dia,
                    COALESCE(SUM(CASE WHEN fecha >= date_trunc('week', now()) THEN total END),0) AS semana,
                    COALESCE(SUM(CASE WHEN fecha >= date_trunc('month', now()) THEN total END),0) AS mes,
                    COALESCE(SUM(CASE WHEN fecha >= date_trunc('year', now()) THEN total END),0) AS anio
                FROM venta;", conn))
            {
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    result.TotalVendido = reader.GetDecimal(0);
                    result.VentasDia = reader.GetDecimal(1);
                    result.VentasSemana = reader.GetDecimal(2);
                    result.VentasMes = reader.GetDecimal(3);
                    result.VentasAnio = reader.GetDecimal(4);
                }
            }

            using (var cmdMargen = new NpgsqlCommand(@"
                WITH avg_compra AS (
                    SELECT producto_id, AVG(precio_compra) AS avg_precio
                    FROM producto_stock_mov
                    WHERE tipo = 'INGRESO' AND precio_compra IS NOT NULL
                    GROUP BY producto_id
                ),
                ventas_costo AS (
                    SELECT vd.producto_id,
                           SUM(vd.cantidad) AS cantidad,
                           SUM(vd.subtotal) AS total_venta,
                           ac.avg_precio AS avg_costo
                    FROM venta_detalle vd
                    JOIN venta v ON v.id = vd.venta_id
                    JOIN avg_compra ac ON ac.producto_id = vd.producto_id
                    GROUP BY vd.producto_id, ac.avg_precio
                )
                SELECT COALESCE(SUM(total_venta),0) AS total_venta,
                       COALESCE(SUM(cantidad * avg_costo),0) AS costo_total
                FROM ventas_costo;", conn))
            {
                using var r = cmdMargen.ExecuteReader();
                if (r.Read())
                {
                    var totalVentasConCosto = r.GetDecimal(0);
                    var costoProductos = r.GetDecimal(1);
                    if (totalVentasConCosto > 0)
                    {
                        result.MargenBrutoPorcentaje = Math.Round(((totalVentasConCosto - costoProductos) / totalVentasConCosto) * 100m, 2);
                    }
                    else
                    {
                        result.MargenBrutoPorcentaje = 0m;
                    }
                }
            }
        }

        private void CargarTopProductos(NpgsqlConnection conn, ReporteFinancieroResumen result, int? diasTopProductos, long? categoriaTopProductosId)
        {
            // Top productos se calculan por cantidad vendida para priorizar rotacion
            var restringirPorFecha = diasTopProductos.HasValue && diasTopProductos.Value > 0;
            var restringirPorCategoria = categoriaTopProductosId.HasValue && categoriaTopProductosId.Value > 0;
            var condiciones = new List<string>();
            if (restringirPorFecha)
            {
                condiciones.Add("v.fecha >= @fechaDesde");
            }
            if (restringirPorCategoria)
            {
                condiciones.Add("p.categoria_id = @categoriaId");
            }

            var sql = @"
                SELECT vd.producto_id,
                       COALESCE(p.nombre, '#' || vd.producto_id::text) AS nombre,
                       SUM(vd.cantidad) AS cantidad,
                       SUM(vd.subtotal) AS total
                FROM venta_detalle vd
                JOIN venta v ON v.id = vd.venta_id
                LEFT JOIN producto p ON p.id = vd.producto_id";

            if (condiciones.Count > 0)
            {
                sql += "\n                WHERE " + string.Join("\n                AND ", condiciones);
            }

            sql += @"
                GROUP BY vd.producto_id, nombre
                ORDER BY cantidad DESC
                LIMIT 5;";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (restringirPorFecha)
            {
                var fechaDesde = DateTime.Now.Date.AddDays(-diasTopProductos!.Value);
                cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
            }
            if (restringirPorCategoria)
            {
                cmd.Parameters.AddWithValue("@categoriaId", categoriaTopProductosId!.Value);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.TopProductos.Add(new ReporteFinancieroTopProducto
                {
                    ProductoId = reader.GetInt64(0),
                    Nombre = reader.GetString(1),
                    CantidadVendida = reader.GetDecimal(2),
                    ImporteTotal = reader.GetDecimal(3)
                });
            }
        }

        private void CargarTopClientes(NpgsqlConnection conn, ReporteFinancieroResumen result, int? diasTopClientes)
        {
            // Clientes destacados se ordenan por importe total facturado
            var restringirPorFecha = diasTopClientes.HasValue && diasTopClientes.Value > 0;
            var sql = @"
                SELECT v.cliente_id,
                       COALESCE(NULLIF(TRIM(COALESCE(c.nombre,'') || ' ' || COALESCE(c.apellido,'')), ''), 'Consumidor Final') AS nombre,
                       COUNT(*) AS compras,
                       SUM(v.total) AS total
                FROM venta v
                LEFT JOIN cliente c ON c.id = v.cliente_id
                WHERE v.cliente_id IS NOT NULL";

            if (restringirPorFecha)
            {
                sql += "\n                AND v.fecha >= @fechaDesdeClientes";
            }

            sql += @"
                GROUP BY v.cliente_id, COALESCE(NULLIF(TRIM(COALESCE(c.nombre,'') || ' ' || COALESCE(c.apellido,'')), ''), 'Consumidor Final')
                ORDER BY total DESC
                LIMIT 5;";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (restringirPorFecha)
            {
                var fechaDesde = DateTime.Now.Date.AddDays(-diasTopClientes!.Value);
                cmd.Parameters.AddWithValue("@fechaDesdeClientes", fechaDesde);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.TopClientes.Add(new ReporteFinancieroTopCliente
                {
                    ClienteId = reader.GetInt64(0),
                    Nombre = reader.GetString(1),
                    CantidadCompras = reader.GetInt32(2),
                    ImporteTotal = reader.GetDecimal(3)
                });
            }
        }

        private void CargarDeudores(NpgsqlConnection conn, ReporteFinancieroResumen result)
        {
            using var cmd = new NpgsqlCommand(@"
                WITH saldos AS (
                    SELECT cliente_id,
                        COALESCE(SUM(CASE WHEN tipo IN ('DEUDA','NOTA_DEBITO') THEN monto ELSE 0 END),0) -
                        COALESCE(SUM(CASE WHEN tipo IN ('PAGO','NOTA_CREDITO') THEN monto ELSE 0 END),0) AS saldo
                    FROM cliente_cuenta_corriente_mov
                    GROUP BY cliente_id
                )
                SELECT c.id, COALESCE(NULLIF(TRIM(c.nombre || ' ' || COALESCE(c.apellido,'')), ''), '#' || c.id::text) AS nombre,
                       COALESCE(s.saldo,0) AS saldo, c.limite_credito
                FROM cliente c
                JOIN saldos s ON s.cliente_id = c.id
                WHERE s.saldo > 0
                ORDER BY s.saldo DESC;", conn);

            using var reader = cmd.ExecuteReader();
            decimal totalDeuda = 0;
            while (reader.Read())
            {
                var deuda = reader.GetDecimal(2);
                totalDeuda += deuda;
                result.Deudores.Add(new ReporteFinancieroDeudor
                {
                    ClienteId = reader.GetInt64(0),
                    Nombre = reader.GetString(1),
                    DeudaTotal = deuda,
                    LimiteCredito = reader.GetDecimal(3)
                });
            }
            result.DeudaTotal = totalDeuda;
        }

        private double? CalcularPromedioCobroDias(NpgsqlConnection conn)
        {
            using var cmd = new NpgsqlCommand(@"
                SELECT AVG(EXTRACT(EPOCH FROM (p.fecha - d.fecha)) / 86400.0) AS dias
                FROM cliente_cuenta_corriente_mov p
                JOIN cliente_cuenta_corriente_mov d ON d.id = p.movimiento_relacionado_id
                WHERE p.tipo = 'PAGO' AND d.tipo = 'DEUDA';", conn);

            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return null;
            return Convert.ToDouble(res);
        }
    }
}
