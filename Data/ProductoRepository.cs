using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;
using System.Linq;

namespace mi_ferreteria.Data
{
    public class ProductoRepository : IProductoRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductoRepository> _logger;

        public ProductoRepository(IConfiguration configuration, ILogger<ProductoRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        public IEnumerable<Producto> GetAll()
        {
            var list = new List<Producto>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"SELECT id, sku, nombre, descripcion, categoria_id,
                                                           precio_venta_actual, stock_minimo, unidad_medida, activo,
                                                           ubicacion_preferida_id, ubicacion_codigo, created_at, updated_at
                                                    FROM producto
                                                    ORDER BY id DESC", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar productos");
                throw;
            }
        }

        public IEnumerable<Producto> GetPage(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var list = new List<Producto>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                var sql = @"SELECT id, sku, nombre, descripcion, categoria_id,
                                     precio_venta_actual, stock_minimo, unidad_medida, activo,
                                     ubicacion_preferida_id, ubicacion_codigo, created_at, updated_at
                              FROM producto
                              ORDER BY id DESC
                              LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al paginar productos");
                throw;
            }
        }

        private static string BuildOrderBy(string sort)
        {
            // Map sort keys to SQL expressions. Only allow known values to avoid injection.
            return sort switch
            {
                "id_asc" => "p.id ASC",
                "id_desc" => "p.id DESC",
                "nombre_asc" => "p.nombre ASC, p.id DESC",
                "nombre_desc" => "p.nombre DESC, p.id DESC",
                "precio_asc" => "p.precio_venta_actual ASC, p.id DESC",
                "precio_desc" => "p.precio_venta_actual DESC, p.id DESC",
                "stock_asc" => "COALESCE(s.cantidad,0) ASC, p.id DESC",
                "stock_desc" => "COALESCE(s.cantidad,0) DESC, p.id DESC",
                _ => "p.id DESC",
            };
        }

        public IEnumerable<Producto> GetPageSorted(int page, int pageSize, string sort)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var list = new List<Producto>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                var orderBy = BuildOrderBy(sort);
                var sql = $@"SELECT p.id, p.sku, p.nombre, p.descripcion, p.categoria_id,
                                     p.precio_venta_actual, p.stock_minimo, p.unidad_medida, p.activo,
                                     p.ubicacion_preferida_id, p.ubicacion_codigo, p.created_at, p.updated_at
                              FROM producto p
                              LEFT JOIN producto_stock s ON s.producto_id = p.id
                              ORDER BY {orderBy}
                              LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al paginar productos con orden");
                throw;
            }
        }

        public int CountAll()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM producto", conn);
                var obj = cmd.ExecuteScalar();
                return obj is long l ? (int)l : Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar productos");
                throw;
            }
        }

        public int CountSearch(string query)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using (var ext = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS unaccent", conn)) { ext.ExecuteNonQuery(); }
                var sql = @"SELECT COUNT(1)
                             FROM producto p
                             WHERE (lower(p.sku) LIKE unaccent(lower(@q))
                                 OR lower(p.nombre) LIKE unaccent(lower(@q))
                                 OR lower(p.descripcion) LIKE unaccent(lower(@q))
                                 OR lower(p.ubicacion_codigo) LIKE unaccent(lower(@q))
                                 OR EXISTS (SELECT 1 FROM producto_codigo_barra b WHERE b.producto_id = p.id AND lower(b.codigo_barra) LIKE unaccent(lower(@q)))
                                 OR EXISTS (SELECT 1 FROM categoria c WHERE c.id = p.categoria_id AND lower(c.nombre) LIKE unaccent(lower(@q)))
                                 OR EXISTS (SELECT 1 FROM producto_categoria pc JOIN categoria c2 ON c2.id=pc.categoria_id WHERE pc.producto_id=p.id AND lower(c2.nombre) LIKE unaccent(lower(@q)))
                              )";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                var obj = cmd.ExecuteScalar();
                return obj is long l ? (int)l : Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar resultados de búsqueda de productos");
                throw;
            }
        }

        public IEnumerable<Producto> SearchPage(string query, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var list = new List<Producto>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                var sql = @"SELECT p.id, p.sku, p.nombre, p.descripcion, p.categoria_id,
                                     p.precio_venta_actual, p.stock_minimo, p.unidad_medida, p.activo,
                                     p.ubicacion_preferida_id, p.ubicacion_codigo, p.created_at, p.updated_at
                              FROM producto p
                              WHERE (lower(p.sku) LIKE unaccent(lower(@q))
                                  OR lower(p.nombre) LIKE unaccent(lower(@q))
                                  OR lower(p.descripcion) LIKE unaccent(lower(@q))
                                  OR lower(p.ubicacion_codigo) LIKE unaccent(lower(@q))
                                  OR EXISTS (SELECT 1 FROM producto_codigo_barra b WHERE b.producto_id = p.id AND lower(b.codigo_barra) LIKE unaccent(lower(@q)))
                                  OR EXISTS (SELECT 1 FROM categoria c WHERE c.id = p.categoria_id AND lower(c.nombre) LIKE unaccent(lower(@q)))
                                  OR EXISTS (SELECT 1 FROM producto_categoria pc JOIN categoria c2 ON c2.id=pc.categoria_id WHERE pc.producto_id=p.id AND lower(c2.nombre) LIKE unaccent(lower(@q)))
                               )
                              ORDER BY p.id DESC
                              LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar productos paginados");
                throw;
            }
        }

        public IEnumerable<Producto> SearchPageSorted(string query, int page, int pageSize, string sort)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var list = new List<Producto>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                var orderBy = BuildOrderBy(sort);
                var sql = $@"SELECT p.id, p.sku, p.nombre, p.descripcion, p.categoria_id,
                                     p.precio_venta_actual, p.stock_minimo, p.unidad_medida, p.activo,
                                     p.ubicacion_preferida_id, p.ubicacion_codigo, p.created_at, p.updated_at
                              FROM producto p
                              LEFT JOIN producto_stock s ON s.producto_id = p.id
                              WHERE (lower(p.sku) LIKE unaccent(lower(@q))
                                  OR lower(p.nombre) LIKE unaccent(lower(@q))
                                  OR lower(p.descripcion) LIKE unaccent(lower(@q))
                                  OR lower(p.ubicacion_codigo) LIKE unaccent(lower(@q))
                                  OR EXISTS (SELECT 1 FROM producto_codigo_barra b WHERE b.producto_id = p.id AND lower(b.codigo_barra) LIKE unaccent(lower(@q)))
                                  OR EXISTS (SELECT 1 FROM categoria c WHERE c.id = p.categoria_id AND lower(c.nombre) LIKE unaccent(lower(@q)))
                                  OR EXISTS (SELECT 1 FROM producto_categoria pc JOIN categoria c2 ON c2.id=pc.categoria_id WHERE pc.producto_id=p.id AND lower(c2.nombre) LIKE unaccent(lower(@q)))
                              
                                  OR EXISTS (SELECT 1 FROM categoria c WHERE c.id = p.categoria_id AND lower(c.nombre) LIKE unaccent(lower(@q))))
                              ORDER BY {orderBy}
                              LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar productos con orden");
                throw;
            }
        }

        public Producto? GetById(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"SELECT id, sku, nombre, descripcion, categoria_id,
                                                           precio_venta_actual, stock_minimo, unidad_medida, activo,
                                                           ubicacion_preferida_id, ubicacion_codigo, created_at, updated_at
                                                    FROM producto WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return MapProducto(reader);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener producto {ProductoId}", id);
                throw;
            }
        }

        public void Add(Producto p)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"INSERT INTO producto
                    (sku, nombre, descripcion, categoria_id, precio_venta_actual, stock_minimo, unidad_medida, activo, ubicacion_preferida_id, ubicacion_codigo)
                    VALUES (@sku, @nombre, @descripcion, @categoria_id, @precio, @stockmin, @unidad, @activo, @ubipref, @ubicod)
                    RETURNING id, created_at, updated_at", conn);
                cmd.Parameters.AddWithValue("@sku", p.Sku);
                cmd.Parameters.AddWithValue("@nombre", p.Nombre);
                cmd.Parameters.AddWithValue("@descripcion", (object?)p.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@categoria_id", (object?)p.CategoriaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@precio", p.PrecioVentaActual);
                cmd.Parameters.AddWithValue("@stockmin", p.StockMinimo);
                cmd.Parameters.AddWithValue("@unidad", p.UnidadMedida);
                cmd.Parameters.AddWithValue("@activo", p.Activo);
                cmd.Parameters.AddWithValue("@ubipref", (object?)p.UbicacionPreferidaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ubicod", (object?)p.UbicacionCodigo ?? DBNull.Value);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    p.Id = reader.GetInt64(0);
                    p.CreatedAt = reader.GetFieldValue<DateTimeOffset>(1);
                    p.UpdatedAt = reader.GetFieldValue<DateTimeOffset>(2);
                }
            }
            catch (PostgresException pg) when (pg.SqlState == "23505")
            {
                _logger.LogWarning(pg, "Violación de unicidad al crear producto con SKU {Sku}", p.Sku);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto {@Producto}", p);
                throw;
            }
        }

        public void Update(Producto p)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"UPDATE producto SET
                        sku=@sku, nombre=@nombre, descripcion=@descripcion, categoria_id=@categoria_id,
                        precio_venta_actual=@precio, stock_minimo=@stockmin, unidad_medida=@unidad, activo=@activo, ubicacion_preferida_id=@ubipref, ubicacion_codigo=@ubicod
                    WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", p.Id);
                cmd.Parameters.AddWithValue("@sku", p.Sku);
                cmd.Parameters.AddWithValue("@nombre", p.Nombre);
                cmd.Parameters.AddWithValue("@descripcion", (object?)p.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@categoria_id", (object?)p.CategoriaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@precio", p.PrecioVentaActual);
                cmd.Parameters.AddWithValue("@stockmin", p.StockMinimo);
                cmd.Parameters.AddWithValue("@unidad", p.UnidadMedida);
                cmd.Parameters.AddWithValue("@activo", p.Activo);
                cmd.Parameters.AddWithValue("@ubipref", (object?)p.UbicacionPreferidaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ubicod", (object?)p.UbicacionCodigo ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch (PostgresException pg) when (pg.SqlState == "23505")
            {
                _logger.LogWarning(pg, "Violación de unicidad al actualizar producto {ProductoId} con SKU {Sku}", p.Id, p.Sku);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar producto {@Producto}", p);
                throw;
            }
        }

        public void Delete(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand("DELETE FROM producto WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar producto {ProductoId}", id);
                throw;
            }
        }

        public bool SkuExists(string sku, long? excludeId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                var sql = "SELECT EXISTS(SELECT 1 FROM producto WHERE lower(sku)=lower(@sku)" + (excludeId.HasValue ? " AND id<>@id)" : ")");
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sku", sku);
                if (excludeId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@id", excludeId.Value);
                }
                var result = cmd.ExecuteScalar();
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando existencia de SKU {Sku}", sku);
                throw;
            }
        }

        public IEnumerable<ProductoCodigoBarra> GetBarcodes(long productoId)
        {
            var list = new List<ProductoCodigoBarra>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand(@"SELECT id, producto_id, codigo_barra, tipo
                                                   FROM producto_codigo_barra WHERE producto_id=@id ORDER BY id", conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new ProductoCodigoBarra
                    {
                        Id = r.GetInt64(0),
                        ProductoId = r.GetInt64(1),
                        CodigoBarra = r.GetString(2),
                        Tipo = r.IsDBNull(3) ? null : r.GetString(3)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener códigos de barra del producto {ProductoId}", productoId);
                throw;
            }
        }

        public void ReplaceBarcodes(long productoId, IEnumerable<ProductoCodigoBarra> codigos)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var tx = conn.BeginTransaction();
                using (var del = new NpgsqlCommand("DELETE FROM producto_codigo_barra WHERE producto_id=@id", conn, tx))
                {
                    del.Parameters.AddWithValue("@id", productoId);
                    del.ExecuteNonQuery();
                }
                foreach (var cb in codigos.Where(c => !string.IsNullOrWhiteSpace(c.CodigoBarra)))
                {
                    using var ins = new NpgsqlCommand("INSERT INTO producto_codigo_barra (producto_id, codigo_barra, tipo) VALUES (@pid, @code, @tipo)", conn, tx);
                    ins.Parameters.AddWithValue("@pid", productoId);
                    ins.Parameters.AddWithValue("@code", cb.CodigoBarra.Trim());
                    ins.Parameters.AddWithValue("@tipo", (object?)cb.Tipo ?? DBNull.Value);
                    ins.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch (PostgresException pg) when (pg.SqlState == "23505")
            {
                _logger.LogWarning(pg, "Código de barra duplicado al guardar producto {ProductoId}", productoId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar códigos de barra del producto {ProductoId}", productoId);
                throw;
            }
        }

        public bool BarcodeExists(string codigo, long? excludeProductId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                var sql = "SELECT EXISTS(SELECT 1 FROM producto_codigo_barra WHERE codigo_barra=@c" + (excludeProductId.HasValue ? " AND producto_id<>@id)" : ")");
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@c", codigo);
                if (excludeProductId.HasValue) cmd.Parameters.AddWithValue("@id", excludeProductId.Value);
                var res = cmd.ExecuteScalar();
                return res is bool b && b;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando código de barra {Codigo}", codigo);
                throw;
            }
        }
        public int CountInactive()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM producto WHERE activo = false", conn);
                var obj = cmd.ExecuteScalar();
                return obj is long l ? (int)l : Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar productos inactivos");
                throw;
            }
        }

        public IEnumerable<Producto> GetLastCreated(int top)
        {
            var list = new List<Producto>();
            if (top < 1) top = 5;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"
                    SELECT id, sku, nombre, descripcion, categoria_id,
                           precio_venta_actual, stock_minimo, unidad_medida, activo,
                           ubicacion_preferida_id, ubicacion_codigo, created_at, updated_at
                    FROM producto
                    ORDER BY created_at DESC, id DESC
                    LIMIT @top", conn);
                cmd.Parameters.AddWithValue("@top", top);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ultimas altas de productos");
                throw;
            }
        }

        public IEnumerable<Producto> GetLastUpdated(int top)
        {
            var list = new List<Producto>();
            if (top < 1) top = 5;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"
                    SELECT id, sku, nombre, descripcion, categoria_id,
                           precio_venta_actual, stock_minimo, unidad_medida, activo,
                           ubicacion_preferida_id, ubicacion_codigo, created_at, updated_at
                    FROM producto
                    ORDER BY updated_at DESC, id DESC
                    LIMIT @top", conn);
                cmd.Parameters.AddWithValue("@top", top);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ultimas modificaciones de productos");
                throw;
            }
        }

        public IEnumerable<Producto> GetLastInactive(int top)
        {
            var list = new List<Producto>();
            if (top < 1) top = 5;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand(@"
                    SELECT id, sku, nombre, descripcion, categoria_id,
                           precio_venta_actual, stock_minimo, unidad_medida, activo,
                           ubicacion_preferida_id, ubicacion_codigo, created_at, updated_at
                    FROM producto
                    WHERE activo = false
                    ORDER BY updated_at DESC, id DESC
                    LIMIT @top", conn);
                cmd.Parameters.AddWithValue("@top", top);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapProducto(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener productos dados de baja");
                throw;
            }
        }
        
        private static Producto MapProducto(NpgsqlDataReader reader)
        {
            return new Producto
            {
                Id = reader.GetInt64(0),
                Sku = reader.GetString(1),
                Nombre = reader.GetString(2),
                Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                CategoriaId = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4),
                PrecioVentaActual = reader.GetDecimal(5),
                StockMinimo = reader.GetInt32(6),
                UnidadMedida = reader.IsDBNull(7) ? "unidad" : reader.GetString(7),
                Activo = reader.GetBoolean(8),
                UbicacionPreferidaId = reader.IsDBNull(9) ? (long?)null : reader.GetInt64(9),
                UbicacionCodigo = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(11),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(12)
            };
        }

        private void EnsureProductExtras(NpgsqlConnection conn)
        {
            using var set = new NpgsqlCommand("SET search_path TO venta, public", conn);
            set.ExecuteNonQuery();
        }

        public System.Collections.Generic.IEnumerable<long> GetCategorias(long productoId)
        {
            var list = new System.Collections.Generic.List<long>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var cmd = new NpgsqlCommand("SELECT categoria_id FROM producto_categoria WHERE producto_id=@id ORDER BY categoria_id", conn);
                cmd.Parameters.AddWithValue("@id", productoId);
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(r.GetInt64(0));
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorias del producto {ProductoId}", productoId);
                throw;
            }
        }

        public void ReplaceCategorias(long productoId, System.Collections.Generic.IEnumerable<long> categoriaIds)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureProductExtras(conn);
                using var tx = conn.BeginTransaction();
                using (var del = new NpgsqlCommand("DELETE FROM producto_categoria WHERE producto_id=@id", conn, tx))
                { del.Parameters.AddWithValue("@id", productoId); del.ExecuteNonQuery(); }
                foreach (var cid in categoriaIds.Distinct().Take(3))
                {
                    using var ins = new NpgsqlCommand("INSERT INTO producto_categoria (producto_id, categoria_id) VALUES (@pid,@cid)", conn, tx);
                    ins.Parameters.AddWithValue("@pid", productoId);
                    ins.Parameters.AddWithValue("@cid", cid);
                    ins.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reemplazar categorias del producto {ProductoId}", productoId);
                throw;
            }
        }
    }
}


