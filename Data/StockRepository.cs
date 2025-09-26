using System;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace mi_ferreteria.Data
{
    public class StockRepository : IStockRepository
    {
        private readonly string _cs;
        private readonly ILogger<StockRepository> _logger;
        public StockRepository(IConfiguration configuration, ILogger<StockRepository> logger)
        {
            _cs = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        private void EnsureSchema(NpgsqlConnection conn)
        {
            using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
            using var cmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS producto_stock (
                    producto_id BIGINT PRIMARY KEY REFERENCES producto(id) ON DELETE CASCADE,
                    cantidad BIGINT NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS producto_stock_mov (
                    id BIGSERIAL PRIMARY KEY,
                    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
                    producto_id BIGINT NOT NULL REFERENCES producto(id) ON DELETE CASCADE,
                    tipo TEXT NOT NULL,
                    cantidad BIGINT NOT NULL,
                    motivo TEXT
                );
            ", conn);
            cmd.ExecuteNonQuery();
        }

        public long GetStock(long productoId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand("SELECT cantidad FROM producto_stock WHERE producto_id=@id", conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                var obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value) return 0;
                return Convert.ToInt64(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock de producto {ProductoId}", productoId);
                throw;
            }
        }

        public void Ingresar(long productoId, long cantidad, string? motivo = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                using (var up = new NpgsqlCommand(@"INSERT INTO producto_stock (producto_id, cantidad) VALUES (@id, @cant)
                        ON CONFLICT (producto_id) DO UPDATE SET cantidad = producto_stock.cantidad + EXCLUDED.cantidad", conn))
                {
                    up.Parameters.AddWithValue("@id", productoId);
                    up.Parameters.AddWithValue("@cant", cantidad);
                    up.ExecuteNonQuery();
                }
                using (var mov = new NpgsqlCommand("INSERT INTO producto_stock_mov (producto_id, tipo, cantidad, motivo) VALUES (@id,'INGRESO',@cant,@mot)", conn))
                {
                    mov.Parameters.AddWithValue("@id", productoId);
                    mov.Parameters.AddWithValue("@cant", cantidad);
                    mov.Parameters.AddWithValue("@mot", (object?)motivo ?? DBNull.Value);
                    mov.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ingreso de stock {ProductoId} {Cantidad}", productoId, cantidad);
                throw;
            }
        }

        public void Egresar(long productoId, long cantidad, string? motivo = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                using (var up = new NpgsqlCommand(@"INSERT INTO producto_stock (producto_id, cantidad) VALUES (@id, @neg)
                        ON CONFLICT (producto_id) DO UPDATE SET cantidad = producto_stock.cantidad + EXCLUDED.cantidad", conn))
                {
                    up.Parameters.AddWithValue("@id", productoId);
                    up.Parameters.AddWithValue("@neg", -cantidad);
                    up.ExecuteNonQuery();
                }
                using (var mov = new NpgsqlCommand("INSERT INTO producto_stock_mov (producto_id, tipo, cantidad, motivo) VALUES (@id,'EGRESO',@cant,@mot)", conn))
                {
                    mov.Parameters.AddWithValue("@id", productoId);
                    mov.Parameters.AddWithValue("@cant", cantidad);
                    mov.Parameters.AddWithValue("@mot", (object?)motivo ?? DBNull.Value);
                    mov.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en egreso de stock {ProductoId} {Cantidad}", productoId, cantidad);
                throw;
            }
        }

        public System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientos(long productoId, string? tipo = null, int top = 100)
        {
            var list = new System.Collections.Generic.List<mi_ferreteria.Models.StockMovimiento>();
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var sql = "SELECT id, fecha, producto_id, tipo, cantidad, motivo FROM producto_stock_mov WHERE producto_id=@id" + (string.IsNullOrWhiteSpace(tipo) ? "" : " AND tipo=@tipo") + " ORDER BY fecha DESC, id DESC LIMIT @top";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                if (!string.IsNullOrWhiteSpace(tipo)) cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@top", top);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new mi_ferreteria.Models.StockMovimiento
                    {
                        Id = r.GetInt64(0),
                        Fecha = r.GetFieldValue<DateTimeOffset>(1),
                        ProductoId = r.GetInt64(2),
                        Tipo = r.GetString(3),
                        Cantidad = r.GetInt64(4),
                        Motivo = r.IsDBNull(5) ? null : r.GetString(5)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos de stock {ProductoId}", productoId);
                throw;
            }
        }
    }
}
