using System;
using System.Collections.Generic;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly string _cs;
        private readonly ILogger<CategoriaRepository> _logger;

        public CategoriaRepository(IConfiguration configuration, ILogger<CategoriaRepository> logger)
        {
            _cs = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        private void EnsureCategoriaExtras(NpgsqlConnection conn)
        {
            using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
            using var alt = new NpgsqlCommand("ALTER TABLE IF EXISTS categoria ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT TRUE", conn);
            alt.ExecuteNonQuery();
        }

        public IEnumerable<Categoria> GetAll()
        {
            var list = new List<Categoria>();
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("SELECT id, nombre, id_padre, descripcion, activo FROM categoria ORDER BY nombre", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3),
                        Activo = r.GetBoolean(4)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar categorías");
                throw;
            }
        }

        public Categoria? GetById(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("SELECT id, nombre, id_padre, descripcion, activo FROM categoria WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    return new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3),
                        Activo = r.GetBoolean(4)
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categoría {CategoriaId}", id);
                throw;
            }
        }

        public void Add(Categoria c)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("INSERT INTO categoria (nombre, id_padre, descripcion, activo) VALUES (@n, @p, @d, @a) RETURNING id", conn);
                cmd.Parameters.AddWithValue("@n", c.Nombre);
                cmd.Parameters.AddWithValue("@p", (object?)c.IdPadre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@d", (object?)c.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@a", c.Activo);
                var id = cmd.ExecuteScalar();
                if (id != null && id != DBNull.Value) c.Id = Convert.ToInt64(id);
            }
            catch (PostgresException pg) when (pg.SqlState == "23505")
            {
                _logger.LogWarning(pg, "Nombre de categoría duplicado {Nombre}", c.Nombre);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría {@Categoria}", c);
                throw;
            }
        }

        public void Update(Categoria c)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("UPDATE categoria SET nombre=@n, id_padre=@p, descripcion=@d, activo=@a WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", c.Id);
                cmd.Parameters.AddWithValue("@n", c.Nombre);
                cmd.Parameters.AddWithValue("@p", (object?)c.IdPadre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@d", (object?)c.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@a", c.Activo);
                cmd.ExecuteNonQuery();
            }
            catch (PostgresException pg) when (pg.SqlState == "23505")
            {
                _logger.LogWarning(pg, "Nombre de categoría duplicado {Nombre}", c.Nombre);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría {@Categoria}", c);
                throw;
            }
        }

        public void Delete(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("UPDATE categoria SET activo=FALSE WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría {CategoriaId}", id);
                throw;
            }
        }

                public void Activate(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("UPDATE categoria SET activo=TRUE WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar categorA-a {CategoriaId}", id);
                throw;
            }
        }public bool NombreExists(string nombre, long? excludeId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                var sql = "SELECT EXISTS(SELECT 1 FROM categoria WHERE lower(nombre)=lower(@n)" + (excludeId.HasValue ? " AND id<>@id)" : ")");
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@n", nombre);
                if (excludeId.HasValue) cmd.Parameters.AddWithValue("@id", excludeId.Value);
                var res = cmd.ExecuteScalar();
                return res is bool b && b;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando nombre de categoría {Nombre}", nombre);
                throw;
            }
        }


        public void HardDelete(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var tx = conn.BeginTransaction();
                using (var delJoin = new NpgsqlCommand("DELETE FROM producto_categoria WHERE categoria_id=@id", conn, tx))
                { delJoin.Parameters.AddWithValue("@id", id); delJoin.ExecuteNonQuery(); }
                using (var updChildren = new NpgsqlCommand("UPDATE categoria SET id_padre = NULL WHERE id_padre=@id", conn, tx))
                { updChildren.Parameters.AddWithValue("@id", id); updChildren.ExecuteNonQuery(); }
                using (var updProd = new NpgsqlCommand("UPDATE producto SET categoria_id = NULL WHERE categoria_id=@id", conn, tx))
                { updProd.Parameters.AddWithValue("@id", id); updProd.ExecuteNonQuery(); }
                using (var delCat = new NpgsqlCommand("DELETE FROM categoria WHERE id=@id", conn, tx))
                { delCat.Parameters.AddWithValue("@id", id); delCat.ExecuteNonQuery(); }
                tx.Commit();
            }
            catch (PostgresException pg)
            {
                _logger.LogError(pg, "Error de base al eliminar fA-sicamente categorA-a {CategoriaId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar fA-sicamente categorA-a {CategoriaId}", id);
                throw;
            }
        }
        public int CountAll()
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM categoria", conn);
                var res = cmd.ExecuteScalar();
                return res is long l ? (int)l : Convert.ToInt32(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error contando categorías");
                throw;
            }
        }

        public IEnumerable<Categoria> GetPage(int page, int pageSize)
        {
            var list = new List<Categoria>();
            try
            {
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                using var cmd = new NpgsqlCommand("SELECT id, nombre, id_padre, descripcion, activo FROM categoria ORDER BY nombre LIMIT @limit OFFSET @offset", conn);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3),
                        Activo = r.GetBoolean(4)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar página de categorías");
                throw;
            }
        }

        private static string BuildOrderBy(string sort)
        {
            return sort switch
            {
                "id_asc" => "id ASC",
                "id_desc" => "id DESC",
                "nombre_asc" => "nombre ASC, id DESC",
                "nombre_desc" => "nombre DESC, id DESC",
                // PostgreSQL: false < true; DESC muestra Activos primero
                "activo_desc" => "activo DESC, id DESC",
                "activo_asc" => "activo ASC, id DESC",
                _ => "id DESC",
            };
        }

        public IEnumerable<Categoria> GetPageSorted(int page, int pageSize, string sort)
        {
            var list = new List<Categoria>();
            try
            {
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                var orderBy = BuildOrderBy(sort ?? "id_desc");
                var sql = $"SELECT id, nombre, id_padre, descripcion, activo FROM categoria ORDER BY {orderBy} LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3),
                        Activo = r.GetBoolean(4)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar pA�gina de categorA-as con orden");
                throw;
            }
        }
        public int CountSearch(string query)
        {
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM categoria WHERE nombre ILIKE @q", conn);
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                var res = cmd.ExecuteScalar();
                return res is long l ? (int)l : Convert.ToInt32(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error contando categorA-as por bA-usqueda {Query}", query);
                throw;
            }
        }

        public IEnumerable<Categoria> SearchPageSorted(string query, int page, int pageSize, string sort)
        {
            var list = new List<Categoria>();
            try
            {
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                EnsureCategoriaExtras(conn);
                var orderBy = BuildOrderBy(sort ?? "id_desc");
                var sql = $"SELECT id, nombre, id_padre, descripcion, activo FROM categoria WHERE nombre ILIKE @q ORDER BY {orderBy} LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3),
                        Activo = r.GetBoolean(4)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listando categorA-as por bA-usqueda con orden {Query}", query);
                throw;
            }
        }
}



}
