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
            using var set = new NpgsqlCommand("SET search_path TO venta, public", conn);
            set.ExecuteNonQuery();
            using var alt = new NpgsqlCommand("ALTER TABLE IF EXISTS producto_stock_mov ADD COLUMN IF NOT EXISTS precio_compra NUMERIC(18,2)", conn);
            alt.ExecuteNonQuery();
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

        public void Ingresar(long productoId, long cantidad, string motivo, decimal? precioCompra = null)
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
                using (var mov = new NpgsqlCommand("INSERT INTO producto_stock_mov (producto_id, tipo, cantidad, motivo, precio_compra) VALUES (@id,'INGRESO',@cant,@mot,@precio)", conn))
                {
                    mov.Parameters.AddWithValue("@id", productoId);
                    mov.Parameters.AddWithValue("@cant", cantidad);
                    mov.Parameters.AddWithValue("@mot", motivo);
                    if (precioCompra.HasValue)
                        mov.Parameters.AddWithValue("@precio", precioCompra.Value);
                    else
                        mov.Parameters.AddWithValue("@precio", DBNull.Value);
                    mov.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ingreso de stock {ProductoId} {Cantidad}", productoId, cantidad);
                throw;
            }
        }

        public void Egresar(long productoId, long cantidad, string motivo)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                // Validar que el stock no quede negativo
                long actual = 0;
                using (var get = new NpgsqlCommand("SELECT cantidad FROM producto_stock WHERE producto_id=@id", conn))
                {
                    get.Parameters.AddWithValue("@id", productoId);
                    var obj = get.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value) actual = Convert.ToInt64(obj);
                }
                if (cantidad > actual)
                {
                    throw new InvalidOperationException($"No se puede egresar {cantidad}. Stock actual: {actual}.");
                }
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
                    mov.Parameters.AddWithValue("@mot", motivo);
                    mov.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en egreso de stock {ProductoId} {Cantidad}", productoId, cantidad);
                throw;
            }
        }

        public void EgresarPermitiendoNegativo(long productoId, long cantidad, string motivo)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                // No se valida que el stock alcance: puede quedar negativo
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
                    mov.Parameters.AddWithValue("@mot", motivo);
                    mov.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en egreso de stock permitiendo negativo {ProductoId} {Cantidad}", productoId, cantidad);
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
                var sql = "SELECT id, fecha, producto_id, tipo, cantidad, motivo, precio_compra FROM producto_stock_mov WHERE producto_id=@id" + (string.IsNullOrWhiteSpace(tipo) ? "" : " AND tipo=@tipo") + " ORDER BY fecha DESC, id DESC LIMIT @top";
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
                        Motivo = r.IsDBNull(5) ? null : r.GetString(5),
                        PrecioCompra = r.IsDBNull(6) ? (decimal?)null : r.GetDecimal(6)
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

        public System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetUltimosMovimientos(string? tipo = null, int top = 10)
        {
            var list = new System.Collections.Generic.List<mi_ferreteria.Models.StockMovimiento>();
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var sql = "SELECT id, fecha, producto_id, tipo, cantidad, motivo, precio_compra FROM producto_stock_mov" + (string.IsNullOrWhiteSpace(tipo) ? "" : " WHERE tipo=@tipo") + " ORDER BY fecha DESC, id DESC LIMIT @top";
                using var cmd = new NpgsqlCommand(sql, conn);
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
                        Motivo = r.IsDBNull(5) ? null : r.GetString(5),
                        PrecioCompra = r.IsDBNull(6) ? (decimal?)null : r.GetDecimal(6)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ultimos movimientos de stock (tipo={Tipo})", tipo);
                throw;
            }
        }

        public int CountMovimientos(long productoId, string? tipo = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var sql = "SELECT COUNT(1) FROM producto_stock_mov WHERE producto_id=@id" + (string.IsNullOrWhiteSpace(tipo) ? "" : " AND tipo=@tipo");
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                if (!string.IsNullOrWhiteSpace(tipo)) cmd.Parameters.AddWithValue("@tipo", tipo);
                var res = cmd.ExecuteScalar();
                return res is long l ? (int)l : Convert.ToInt32(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error contando movimientos de stock {ProductoId}", productoId);
                throw;
            }
        }

        public System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientosPage(long productoId, string? tipo, int page, int pageSize)
        {
            var list = new System.Collections.Generic.List<mi_ferreteria.Models.StockMovimiento>();
            try
            {
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var sql = "SELECT id, fecha, producto_id, tipo, cantidad, motivo, precio_compra FROM producto_stock_mov WHERE producto_id=@id" + (string.IsNullOrWhiteSpace(tipo) ? "" : " AND tipo=@tipo") + " ORDER BY fecha DESC, id DESC LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                if (!string.IsNullOrWhiteSpace(tipo)) cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
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
                        Motivo = r.IsDBNull(5) ? null : r.GetString(5),
                        PrecioCompra = r.IsDBNull(6) ? (decimal?)null : r.GetDecimal(6)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener página de movimientos de stock {ProductoId}", productoId);
                throw;
            }
        }

        public int CountMovimientosGlobal(string? tipo = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var sql = "SELECT COUNT(1) FROM producto_stock_mov" + (string.IsNullOrWhiteSpace(tipo) ? "" : " WHERE tipo=@tipo");
                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(tipo)) cmd.Parameters.AddWithValue("@tipo", tipo);
                var res = cmd.ExecuteScalar();
                return res is long l ? (int)l : Convert.ToInt32(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error contando movimientos globales de stock (tipo={Tipo})", tipo);
                throw;
            }
        }

        public System.Collections.Generic.IDictionary<long, long> GetStocks(System.Collections.Generic.IEnumerable<long> productoIds)
        {
            var result = new System.Collections.Generic.Dictionary<long, long>();
            var ids = productoIds?.Distinct().ToArray();
            if (ids == null || ids.Length == 0) return result;

            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand("SELECT producto_id, cantidad FROM producto_stock WHERE producto_id = ANY(@ids)", conn);
                cmd.Parameters.AddWithValue("@ids", ids);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt64(0);
                    var cant = reader.GetInt64(1);
                    result[id] = cant;
                }
                // productos sin fila en producto_stock quedan con 0 (no se agregan)
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stocks para múltiples productos");
                throw;
            }
        }

        public System.Collections.Generic.IEnumerable<mi_ferreteria.Models.StockMovimiento> GetMovimientosGlobalPage(string? tipo, int page, int pageSize)
        {
            var list = new System.Collections.Generic.List<mi_ferreteria.Models.StockMovimiento>();
            try
            {
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var sql = "SELECT id, fecha, producto_id, tipo, cantidad, motivo, precio_compra FROM producto_stock_mov" + (string.IsNullOrWhiteSpace(tipo) ? "" : " WHERE tipo=@tipo") + " ORDER BY fecha DESC, id DESC LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(tipo)) cmd.Parameters.AddWithValue("@tipo", tipo);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
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
                        Motivo = r.IsDBNull(5) ? null : r.GetString(5),
                        PrecioCompra = r.IsDBNull(6) ? (decimal?)null : r.GetDecimal(6)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo pagina de movimientos globales de stock (tipo={Tipo})", tipo);
                throw;
            }
        }

        public decimal? GetUltimoPrecioCompra(long productoId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"SELECT precio_compra
                                                    FROM producto_stock_mov
                                                    WHERE producto_id=@id AND tipo='INGRESO' AND precio_compra IS NOT NULL
                                                    ORDER BY fecha DESC, id DESC
                                                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value) return null;
                return Convert.ToDecimal(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo ultimo precio de compra {ProductoId}", productoId);
                throw;
            }
        }

        public System.Collections.Generic.IEnumerable<mi_ferreteria.Models.ProductoStockCritico> GetProductosStockCritico(string? query, int page, int pageSize, out int totalCount)
        {
            var result = new System.Collections.Generic.List<mi_ferreteria.Models.ProductoStockCritico>();
            totalCount = 0;
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                var offset = (page - 1) * pageSize;
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureSchema(conn);
                var filtros = "p.activo = true AND p.stock_minimo > 0 AND COALESCE(ps.cantidad,0) <= p.stock_minimo";
                var tieneFiltro = !string.IsNullOrWhiteSpace(query);
                if (tieneFiltro)
                {
                    filtros += " AND (p.sku ILIKE @q OR p.nombre ILIKE @q OR COALESCE(p.descripcion,'') ILIKE @q OR COALESCE(p.ubicacion_codigo,'') ILIKE @q)";
                }
                using (var countCmd = new NpgsqlCommand($@"SELECT COUNT(1)
                                                           FROM producto p
                                                           LEFT JOIN producto_stock ps ON ps.producto_id = p.id
                                                           WHERE {filtros}", conn))
                {
                    if (tieneFiltro)
                    {
                        countCmd.Parameters.AddWithValue("@q", $"%{query!.Trim()}%");
                    }
                    var scalar = countCmd.ExecuteScalar();
                    totalCount = scalar is long l ? (int)l : Convert.ToInt32(scalar);
                }

                using var cmd = new NpgsqlCommand($@"
                    SELECT p.id, p.sku, p.nombre,
                           COALESCE(ps.cantidad,0) AS stock_actual,
                           p.stock_minimo,
                           COALESCE(p.unidad_medida,'unidad') AS unidad,
                           p.ubicacion_codigo,
                           p.precio_venta_actual
                    FROM producto p
                    LEFT JOIN producto_stock ps ON ps.producto_id = p.id
                    WHERE {filtros}
                    ORDER BY COALESCE(ps.cantidad,0) ASC, p.nombre ASC
                    LIMIT @limit OFFSET @offset", conn);
                if (tieneFiltro)
                {
                    cmd.Parameters.AddWithValue("@q", $"%{query!.Trim()}%");
                }
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new mi_ferreteria.Models.ProductoStockCritico
                    {
                        Id = reader.GetInt64(0),
                        Sku = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Nombre = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        StockActual = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        StockMinimo = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        UnidadMedida = reader.IsDBNull(5) ? "unidad" : reader.GetString(5),
                        UbicacionCodigo = reader.IsDBNull(6) ? null : reader.GetString(6),
                        PrecioVentaActual = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7)
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener productos con stock crítico");
                throw;
            }
        }
    }
}
