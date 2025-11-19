using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public class VentaRepository : IVentaRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<VentaRepository> _logger;

        public VentaRepository(IConfiguration configuration, ILogger<VentaRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        private void EnsureSchema(NpgsqlConnection conn)
        {
            using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn))
            {
                set.ExecuteNonQuery();
            }

            using (var cmd = new NpgsqlCommand(@"
                -- Tablas de ventas
                CREATE TABLE IF NOT EXISTS venta (
                    id BIGSERIAL PRIMARY KEY,
                    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
                    cliente_id BIGINT NULL,
                    tipo_cliente TEXT NOT NULL,
                    tipo_pago TEXT NOT NULL,
                    total NUMERIC(12,2) NOT NULL,
                    total_en_letras TEXT NOT NULL,
                    usuario_id INT NOT NULL,
                    estado TEXT NOT NULL DEFAULT 'CONFIRMADA',
                    observaciones TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS venta_detalle (
                    id BIGSERIAL PRIMARY KEY,
                    venta_id BIGINT NOT NULL REFERENCES venta(id) ON DELETE CASCADE,
                    producto_id BIGINT NOT NULL,
                    descripcion TEXT NOT NULL,
                    cantidad NUMERIC(12,3) NOT NULL,
                    precio_unitario NUMERIC(12,2) NOT NULL,
                    subtotal NUMERIC(12,2) NOT NULL,
                    permite_venta_sin_stock BOOLEAN NOT NULL DEFAULT false
                );

                CREATE TABLE IF NOT EXISTS venta_pago (
                    id BIGSERIAL PRIMARY KEY,
                    venta_id BIGINT NOT NULL REFERENCES venta(id) ON DELETE CASCADE,
                    tipo TEXT NOT NULL,
                    monto NUMERIC(12,2) NOT NULL,
                    detalle TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS factura (
                    id BIGSERIAL PRIMARY KEY,
                    venta_id BIGINT NOT NULL UNIQUE REFERENCES venta(id) ON DELETE CASCADE,
                    tipo_comprobante TEXT NOT NULL,
                    punto_venta INT NOT NULL DEFAULT 1,
                    numero BIGSERIAL,
                    fecha_emision TIMESTAMPTZ NOT NULL DEFAULT now(),
                    total NUMERIC(12,2) NOT NULL,
                    total_en_letras TEXT NOT NULL,
                    cliente_nombre TEXT NOT NULL,
                    cliente_documento TEXT NULL,
                    cliente_direccion TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS venta_auditoria (
                    id BIGSERIAL PRIMARY KEY,
                    venta_id BIGINT NOT NULL REFERENCES venta(id) ON DELETE CASCADE,
                    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
                    usuario_id INT NOT NULL,
                    accion TEXT NOT NULL,
                    detalle TEXT NULL
                );
            ", conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Asegurar columnas en tablas ya existentes (migración suave)
            try
            {
                using var altDet1 = new NpgsqlCommand("ALTER TABLE venta_detalle ADD COLUMN descripcion TEXT", conn);
                altDet1.ExecuteNonQuery();
            }
            catch (PostgresException ex) when (ex.SqlState == "42701")
            {
                // La columna ya existe
            }

            try
            {
                using var altDet2 = new NpgsqlCommand("ALTER TABLE venta_detalle ADD COLUMN permite_venta_sin_stock BOOLEAN NOT NULL DEFAULT false", conn);
                altDet2.ExecuteNonQuery();
            }
            catch (PostgresException ex) when (ex.SqlState == "42701")
            {
                // La columna ya existe
            }

            // Asegurar columna para caso especial de venta sin stock en producto
            using var altProd = new NpgsqlCommand("ALTER TABLE IF EXISTS producto ADD COLUMN IF NOT EXISTS permite_venta_sin_stock BOOLEAN NOT NULL DEFAULT false", conn);
            altProd.ExecuteNonQuery();
        }

        private static string NumeroEnLetras(decimal valor)
        {
            var entero = (long)Math.Truncate(valor);
            var centavos = (int)((valor - entero) * 100);
            if (centavos < 0) centavos = -centavos;

            string LetrasEntero(long n)
            {
                if (n == 0) return "cero";
                if (n < 0) return "menos " + LetrasEntero(-n);

                string[] unidades =
                {
                    "", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
                    "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve"
                };
                string[] decenas = { "", "diez", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };
                string[] centenas =
                {
                    "", "cien", "doscientos", "trescientos", "cuatrocientos", "quinientos",
                    "seiscientos", "setecientos", "ochocientos", "novecientos"
                };

                if (n < 20) return unidades[n];
                if (n < 100)
                {
                    var d = n / 10;
                    var r = n % 10;
                    if (n == 20) return "veinte";
                    if (n < 30) return "veinti" + unidades[r];
                    return decenas[d] + (r > 0 ? " y " + unidades[r] : "");
                }
                if (n < 1000)
                {
                    var c = n / 100;
                    var r = n % 100;
                    if (n == 100) return "cien";
                    return centenas[c] + (r > 0 ? " " + LetrasEntero(r) : "");
                }
                if (n < 1_000_000)
                {
                    var m = n / 1000;
                    var r = n % 1000;
                    var pref = m == 1 ? "mil" : LetrasEntero(m) + " mil";
                    return pref + (r > 0 ? " " + LetrasEntero(r) : "");
                }
                if (n < 1_000_000_000_000L)
                {
                    var mi = n / 1_000_000;
                    var r = n % 1_000_000;
                    var pref = mi == 1 ? "un millón" : LetrasEntero(mi) + " millones";
                    return pref + (r > 0 ? " " + LetrasEntero(r) : "");
                }
                return n.ToString();
            }

            var textoEntero = LetrasEntero(entero);
            return $"{textoEntero} con {centavos:00}/100";
        }

        public Venta CrearVenta(Venta venta, IEnumerable<VentaDetalle> detalles, bool registrarFactura, Cliente? cliente, string tipoComprobante)
        {
            if (venta == null) throw new ArgumentNullException(nameof(venta));
            if (detalles == null) throw new ArgumentNullException(nameof(detalles));

            var listaDetalles = detalles.ToList();
            if (listaDetalles.Count == 0) throw new InvalidOperationException("La venta debe tener al menos un detalle.");

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var tx = conn.BeginTransaction();

                var total = listaDetalles.Sum(d => d.Subtotal);
                venta.Total = total;
                venta.TotalEnLetras = NumeroEnLetras(total);

                using (var cmdVenta = new NpgsqlCommand(@"
                    INSERT INTO venta (fecha, cliente_id, tipo_cliente, tipo_pago, total, total_en_letras, usuario_id, estado, observaciones)
                    VALUES (@fecha, @cid, @tcli, @tpago, @total, @total_txt, @uid, @estado, @obs)
                    RETURNING id, fecha", conn, tx))
                {
                    var fecha = venta.Fecha == default ? DateTimeOffset.UtcNow : venta.Fecha.ToUniversalTime();
                    cmdVenta.Parameters.AddWithValue("@fecha", fecha.UtcDateTime);
                    cmdVenta.Parameters.AddWithValue("@cid", (object?)venta.ClienteId ?? DBNull.Value);
                    cmdVenta.Parameters.AddWithValue("@tcli", venta.TipoCliente);
                    cmdVenta.Parameters.AddWithValue("@tpago", venta.TipoPago);
                    cmdVenta.Parameters.AddWithValue("@total", venta.Total);
                    cmdVenta.Parameters.AddWithValue("@total_txt", venta.TotalEnLetras);
                    cmdVenta.Parameters.AddWithValue("@uid", venta.UsuarioId);
                    cmdVenta.Parameters.AddWithValue("@estado", venta.Estado);
                    cmdVenta.Parameters.AddWithValue("@obs", (object?)venta.Observaciones ?? DBNull.Value);
                    using var r = cmdVenta.ExecuteReader();
                    if (r.Read())
                    {
                        venta.Id = r.GetInt64(0);
                        venta.Fecha = r.GetFieldValue<DateTimeOffset>(1);
                    }
                }

                foreach (var d in listaDetalles)
                {
                    using var cmdDet = new NpgsqlCommand(@"
                        INSERT INTO venta_detalle
                            (venta_id, producto_id, descripcion, cantidad, precio_unitario, subtotal, permite_venta_sin_stock)
                        VALUES (@vid, @pid, @desc, @cant, @pu, @sub, @perm)", conn, tx);
                    cmdDet.Parameters.AddWithValue("@vid", venta.Id);
                    cmdDet.Parameters.AddWithValue("@pid", d.ProductoId);
                    cmdDet.Parameters.AddWithValue("@desc", d.Descripcion);
                    cmdDet.Parameters.AddWithValue("@cant", d.Cantidad);
                    cmdDet.Parameters.AddWithValue("@pu", d.PrecioUnitario);
                    cmdDet.Parameters.AddWithValue("@sub", d.Subtotal);
                    cmdDet.Parameters.AddWithValue("@perm", d.PermiteVentaSinStock);
                    cmdDet.ExecuteNonQuery();
                }

                // Pago: por ahora un solo registro por venta
                using (var cmdPago = new NpgsqlCommand(@"
                    INSERT INTO venta_pago (venta_id, tipo, monto, detalle)
                    VALUES (@vid, @tipo, @monto, @det)", conn, tx))
                {
                    cmdPago.Parameters.AddWithValue("@vid", venta.Id);
                    cmdPago.Parameters.AddWithValue("@tipo", venta.TipoPago == "CUENTA_CORRIENTE" ? "CUENTA_CORRIENTE" : "EFECTIVO");
                    cmdPago.Parameters.AddWithValue("@monto", venta.Total);
                    cmdPago.Parameters.AddWithValue("@det", DBNull.Value);
                    cmdPago.ExecuteNonQuery();
                }

                if (registrarFactura)
                {
                    using var cmdFac = new NpgsqlCommand(@"
                        INSERT INTO factura
                            (venta_id, tipo_comprobante, punto_venta, total, total_en_letras, cliente_nombre, cliente_documento, cliente_direccion)
                        VALUES (@vid, @tipo, @pto, @total, @txt, @cnom, @cdoc, @cdir)
                        RETURNING id, numero, fecha_emision", conn, tx);
                    cmdFac.Parameters.AddWithValue("@vid", venta.Id);
                    cmdFac.Parameters.AddWithValue("@tipo", tipoComprobante);
                    cmdFac.Parameters.AddWithValue("@pto", 1);
                    cmdFac.Parameters.AddWithValue("@total", venta.Total);
                    cmdFac.Parameters.AddWithValue("@txt", venta.TotalEnLetras);
                    string nom;
                    if (cliente == null)
                    {
                        nom = "CONSUMIDOR FINAL";
                    }
                    else if (!string.IsNullOrWhiteSpace(cliente.Apellido))
                    {
                        nom = $"{cliente.Nombre} {cliente.Apellido}";
                    }
                    else
                    {
                        nom = cliente.Nombre;
                    }
                    var doc = cliente?.NumeroDocumento ?? (object?)null;
                    var dir = cliente?.Direccion ?? (object?)null;
                    cmdFac.Parameters.AddWithValue("@cnom", nom);
                    cmdFac.Parameters.AddWithValue("@cdoc", (object?)doc ?? DBNull.Value);
                    cmdFac.Parameters.AddWithValue("@cdir", (object?)dir ?? DBNull.Value);
                    using var rf = cmdFac.ExecuteReader();
                    if (rf.Read())
                    {
                        // Valores disponibles si se necesitan en el futuro
                        var _ = rf.GetInt64(0);
                        var __ = rf.GetInt64(1);
                        var ___ = rf.GetFieldValue<DateTimeOffset>(2);
                    }
                }

                using (var cmdAud = new NpgsqlCommand(@"
                    INSERT INTO venta_auditoria (venta_id, usuario_id, accion, detalle)
                    VALUES (@vid, @uid, @acc, @det)", conn, tx))
                {
                    cmdAud.Parameters.AddWithValue("@vid", venta.Id);
                    cmdAud.Parameters.AddWithValue("@uid", venta.UsuarioId);
                    cmdAud.Parameters.AddWithValue("@acc", "CREACION");
                    cmdAud.Parameters.AddWithValue("@det", (object?)"Venta creada desde pantalla de ventas" ?? DBNull.Value);
                    cmdAud.ExecuteNonQuery();
                }

                tx.Commit();
                venta.Detalles = listaDetalles;
                return venta;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta");
                throw;
            }
        }

        public int CountAll()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM venta", conn);
                var res = cmd.ExecuteScalar();
                return res is long l ? (int)l : Convert.ToInt32(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar ventas");
                throw;
            }
        }

        public IEnumerable<Venta> GetPage(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var list = new List<Venta>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                int offset = (page - 1) * pageSize;
                using var cmd = new NpgsqlCommand(@"
                    SELECT id, fecha, cliente_id, tipo_cliente, tipo_pago,
                           total, total_en_letras, usuario_id, estado, observaciones
                    FROM venta
                    ORDER BY fecha DESC, id DESC
                    LIMIT @limit OFFSET @offset", conn);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new Venta
                    {
                        Id = r.GetInt64(0),
                        Fecha = r.GetFieldValue<DateTimeOffset>(1),
                        ClienteId = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        TipoCliente = r.GetString(3),
                        TipoPago = r.GetString(4),
                        Total = r.GetDecimal(5),
                        TotalEnLetras = r.GetString(6),
                        UsuarioId = r.GetInt32(7),
                        Estado = r.GetString(8),
                        Observaciones = r.IsDBNull(9) ? null : r.GetString(9)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener página de ventas");
                throw;
            }
        }

        public (Venta venta, List<VentaDetalle> detalles, Factura? factura)? ObtenerComprobante(long ventaId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);

                Venta? venta = null;
                var detalles = new List<VentaDetalle>();
                Factura? factura = null;

                // Venta
                using (var cmdVenta = new NpgsqlCommand(@"
                    SELECT id, fecha, cliente_id, tipo_cliente, tipo_pago, total, total_en_letras,
                           usuario_id, estado, observaciones
                    FROM venta
                    WHERE id = @id", conn))
                {
                    cmdVenta.Parameters.AddWithValue("@id", ventaId);
                    using var r = cmdVenta.ExecuteReader();
                    if (r.Read())
                    {
                        venta = new Venta
                        {
                            Id = r.GetInt64(0),
                            Fecha = r.GetFieldValue<DateTimeOffset>(1),
                            ClienteId = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                            TipoCliente = r.GetString(3),
                            TipoPago = r.GetString(4),
                            Total = r.GetDecimal(5),
                            TotalEnLetras = r.GetString(6),
                            UsuarioId = r.GetInt32(7),
                            Estado = r.GetString(8),
                            Observaciones = r.IsDBNull(9) ? null : r.GetString(9)
                        };
                    }
                }

                if (venta == null)
                {
                    return null;
                }

                // Detalles
                using (var cmdDet = new NpgsqlCommand(@"
                    SELECT id, venta_id, producto_id, descripcion, cantidad, precio_unitario, subtotal, permite_venta_sin_stock
                    FROM venta_detalle
                    WHERE venta_id = @id
                    ORDER BY id", conn))
                {
                    cmdDet.Parameters.AddWithValue("@id", venta.Id);
                    using var rd = cmdDet.ExecuteReader();
                    while (rd.Read())
                    {
                        detalles.Add(new VentaDetalle
                        {
                            Id = rd.GetInt64(0),
                            VentaId = rd.GetInt64(1),
                            ProductoId = rd.GetInt64(2),
                            Descripcion = rd.GetString(3),
                            Cantidad = rd.GetDecimal(4),
                            PrecioUnitario = rd.GetDecimal(5),
                            Subtotal = rd.GetDecimal(6),
                            PermiteVentaSinStock = rd.GetBoolean(7)
                        });
                    }
                }

                // Factura (si existe)
                using (var cmdFac = new NpgsqlCommand(@"
                    SELECT id, venta_id, tipo_comprobante, punto_venta, numero, fecha_emision,
                           total, total_en_letras, cliente_nombre, cliente_documento, cliente_direccion
                    FROM factura
                    WHERE venta_id = @id", conn))
                {
                    cmdFac.Parameters.AddWithValue("@id", venta.Id);
                    using var rf = cmdFac.ExecuteReader();
                    if (rf.Read())
                    {
                        factura = new Factura
                        {
                            Id = rf.GetInt64(0),
                            VentaId = rf.GetInt64(1),
                            TipoComprobante = rf.GetString(2),
                            PuntoVenta = rf.GetInt32(3),
                            Numero = rf.GetInt64(4),
                            FechaEmision = rf.GetFieldValue<DateTimeOffset>(5),
                            Total = rf.GetDecimal(6),
                            TotalEnLetras = rf.GetString(7),
                            ClienteNombre = rf.GetString(8),
                            ClienteDocumento = rf.IsDBNull(9) ? null : rf.GetString(9),
                            ClienteDireccion = rf.IsDBNull(10) ? null : rf.GetString(10)
                        };
                    }
                }

                venta.Detalles = detalles;
                return (venta, detalles, factura);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comprobante de venta {VentaId}", ventaId);
                throw;
            }
        }
    }
}
