using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using mi_ferreteria.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace mi_ferreteria.Data
{
    public class PermisoRepository : IPermisoRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PermisoRepository> _logger;

        public PermisoRepository(IConfiguration configuration, ILogger<PermisoRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        public List<Permiso> GetByRolIds(List<int> rolIds)
        {
            var permisos = new List<Permiso>();
            try
            {
                if (rolIds == null || rolIds.Count == 0) return permisos;
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(@"SELECT p.id, p.nombre, p.descripcion FROM permiso p
                    JOIN rol_permiso rp ON rp.permiso_id = p.id
                    WHERE rp.rol_id = ANY(@rolIds)", conn);
                cmd.Parameters.AddWithValue("@rolIds", rolIds);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    permisos.Add(new Permiso
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Descripcion = reader.GetString(2)
                    });
                }
                return permisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener permisos por rolIds {@RolIds}", rolIds);
                throw;
            }
        }
        
        public List<Permiso> GetAll()
        {
            var permisos = new List<Permiso>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand("SELECT id, nombre, descripcion FROM permiso ORDER BY nombre", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    permisos.Add(new Permiso
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Descripcion = reader.GetString(2)
                    });
                }
                return permisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los permisos");
                throw;
            }
        }
        
        public List<Permiso> GetPermisosConsolidados(int usuarioId)
        {
            var permisos = new List<Permiso>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                // Select distinct permissions coming from roles OR direct assignment
                using var cmd = new NpgsqlCommand(@"
                    SELECT DISTINCT p.id, p.nombre, p.descripcion 
                    FROM permiso p
                    LEFT JOIN rol_permiso rp ON rp.permiso_id = p.id
                    LEFT JOIN usuario_rol ur ON ur.rol_id = rp.rol_id AND ur.usuario_id = @uid
                    LEFT JOIN usuario_permiso up ON up.permiso_id = p.id AND up.usuario_id = @uid
                    WHERE ur.usuario_id IS NOT NULL OR up.usuario_id IS NOT NULL", conn);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    permisos.Add(new Permiso
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Descripcion = reader.GetString(2)
                    });
                }
                return permisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener permisos consolidados del usuario {UsuarioId}", usuarioId);
                throw;
            }
        }

        public List<Permiso> GetByUsuarioIdDirecto(int usuarioId)
        {
            var permisos = new List<Permiso>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(@"
                    SELECT p.id, p.nombre, p.descripcion 
                    FROM permiso p
                    JOIN usuario_permiso up ON up.permiso_id = p.id
                    WHERE up.usuario_id = @uid", conn);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    permisos.Add(new Permiso
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Descripcion = reader.GetString(2)
                    });
                }
                return permisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener permisos directos del usuario {UsuarioId}", usuarioId);
                throw;
            }
        }

        public void AsignarPermisosDirectos(int usuarioId, List<int> permisoIds)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                
                using (var del = new NpgsqlCommand("DELETE FROM usuario_permiso WHERE usuario_id = @uid", conn, tx))
                {
                    del.Parameters.AddWithValue("@uid", usuarioId);
                    del.ExecuteNonQuery();
                }

                if (permisoIds != null && permisoIds.Count > 0)
                {
                    foreach (var pid in permisoIds)
                    {
                        using var ins = new NpgsqlCommand("INSERT INTO usuario_permiso (usuario_id, permiso_id) VALUES (@uid, @pid)", conn, tx);
                        ins.Parameters.AddWithValue("@uid", usuarioId);
                        ins.Parameters.AddWithValue("@pid", pid);
                        ins.ExecuteNonQuery();
                    }
                }
                
                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar permisos directos al usuario {UsuarioId}", usuarioId);
                throw;
            }
        }
    }
}
