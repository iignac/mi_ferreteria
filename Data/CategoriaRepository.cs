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

        public IEnumerable<Categoria> GetAll()
        {
            var list = new List<Categoria>();
            try
            {
                using var conn = new NpgsqlConnection(_cs);
                conn.Open();
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("SELECT id, nombre, id_padre, descripcion FROM categoria ORDER BY nombre", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3)
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
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("SELECT id, nombre, id_padre, descripcion FROM categoria WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    return new Categoria
                    {
                        Id = r.GetInt64(0),
                        Nombre = r.GetString(1),
                        IdPadre = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3)
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
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("INSERT INTO categoria (nombre, id_padre, descripcion) VALUES (@n, @p, @d) RETURNING id", conn);
                cmd.Parameters.AddWithValue("@n", c.Nombre);
                cmd.Parameters.AddWithValue("@p", (object?)c.IdPadre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@d", (object?)c.Descripcion ?? DBNull.Value);
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
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("UPDATE categoria SET nombre=@n, id_padre=@p, descripcion=@d WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", c.Id);
                cmd.Parameters.AddWithValue("@n", c.Nombre);
                cmd.Parameters.AddWithValue("@p", (object?)c.IdPadre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@d", (object?)c.Descripcion ?? DBNull.Value);
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
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("DELETE FROM categoria WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría {CategoriaId}", id);
                throw;
            }
        }

        public bool NombreExists(string nombre, long? excludeId = null)
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
                using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
                using var cmd = new NpgsqlCommand("SELECT id, nombre, id_padre, descripcion FROM categoria ORDER BY nombre LIMIT @limit OFFSET @offset", conn);
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
                        Descripcion = r.IsDBNull(3) ? null : r.GetString(3)
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
    }
}

