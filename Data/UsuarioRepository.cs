using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;
using mi_ferreteria.Security;

namespace mi_ferreteria.Data
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<UsuarioRepository> _logger;

        public UsuarioRepository(IConfiguration configuration, ILogger<UsuarioRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        public Usuario? GetByEmail(string email, bool includeSecrets = false)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(
                    "SELECT id, nombre, email, activo, password_hash, password_salt FROM usuario WHERE lower(email)=lower(@mail) LIMIT 1",
                    conn);
                cmd.Parameters.AddWithValue("@mail", email);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                var usuario = new Usuario
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Email = reader.GetString(2),
                    Activo = reader.GetBoolean(3),
                    PasswordHash = includeSecrets && !reader.IsDBNull(4) ? (byte[])reader["password_hash"] : null,
                    PasswordSalt = includeSecrets && !reader.IsDBNull(5) ? (byte[])reader["password_salt"] : null
                };

                reader.Close();
                using var cmdRoles = new NpgsqlCommand(@"SELECT r.id, r.nombre, r.descripcion
                                                         FROM usuario_rol ur
                                                         JOIN rol r ON r.id = ur.rol_id
                                                         WHERE ur.usuario_id = @uid", conn);
                cmdRoles.Parameters.AddWithValue("@uid", usuario.Id);
                using var rolesReader = cmdRoles.ExecuteReader();
                var roles = new List<Rol>();
                while (rolesReader.Read())
                {
                    roles.Add(new Rol
                    {
                        Id = rolesReader.GetInt32(0),
                        Nombre = rolesReader.GetString(1),
                        Descripcion = rolesReader.GetString(2)
                    });
                }
                usuario.Roles = roles;
                return usuario;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario por email {Email}", email);
                throw;
            }
        }

        public List<Usuario> GetAll()
        {
            var usuarios = new List<Usuario>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand("SELECT id, nombre, email, activo FROM usuario ORDER BY activo DESC, nombre ASC", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    usuarios.Add(new Usuario
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Email = reader.GetString(2),
                        Activo = reader.GetBoolean(3)
                    });
                }
                // Cargar roles asociados en una sola consulta
                var ids = usuarios.Select(u => u.Id).ToList();
                if (ids.Count > 0)
                {
                    reader.Close();
                    using var cmdRoles = new NpgsqlCommand(@"SELECT ur.usuario_id, r.id, r.nombre, r.descripcion
                                                             FROM usuario_rol ur
                                                             JOIN rol r ON r.id = ur.rol_id
                                                             WHERE ur.usuario_id = ANY(@usuarioIds)", conn);
                    cmdRoles.Parameters.AddWithValue("@usuarioIds", ids);
                    using var rroles = cmdRoles.ExecuteReader();
                    var rolesPorUsuario = new Dictionary<int, List<Rol>>();
                    while (rroles.Read())
                    {
                        var uid = rroles.GetInt32(0);
                        var rol = new Rol
                        {
                            Id = rroles.GetInt32(1),
                            Nombre = rroles.GetString(2),
                            Descripcion = rroles.GetString(3)
                        };
                        if (!rolesPorUsuario.TryGetValue(uid, out var list))
                        {
                            list = new List<Rol>();
                            rolesPorUsuario[uid] = list;
                        }
                        list.Add(rol);
                    }
                    foreach (var u in usuarios)
                    {
                        if (rolesPorUsuario.TryGetValue(u.Id, out var rs))
                            u.Roles = rs;
                    }
                }
                return usuarios;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios");
                throw;
            }
        }

        public void Add(Usuario usuario, string plainPassword)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                var hp = PasswordHasher.HashPassword(plainPassword);
                using var cmd = new NpgsqlCommand("INSERT INTO usuario (nombre, email, activo, password_hash, password_salt) VALUES (@nombre, @email, @activo, @ph, @ps) RETURNING id", conn, tx);
                cmd.Parameters.AddWithValue("@nombre", usuario.Nombre);
                cmd.Parameters.AddWithValue("@email", usuario.Email);
                cmd.Parameters.AddWithValue("@activo", usuario.Activo);
                cmd.Parameters.AddWithValue("@ph", hp.Hash);
                cmd.Parameters.AddWithValue("@ps", hp.Salt);
                var newIdObj = cmd.ExecuteScalar();
                if (newIdObj != null && newIdObj != DBNull.Value)
                {
                    usuario.Id = Convert.ToInt32(newIdObj);
                }

                // Inserta asignaciones de roles si hay
                if (usuario.Roles != null && usuario.Roles.Count > 0)
                {
                    foreach (var rol in usuario.Roles)
                    {
                        using var cmdUr = new NpgsqlCommand("INSERT INTO usuario_rol (usuario_id, rol_id) VALUES (@uid, @rid)", conn, tx);
                        cmdUr.Parameters.AddWithValue("@uid", usuario.Id);
                        cmdUr.Parameters.AddWithValue("@rid", rol.Id);
                        cmdUr.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario {@Usuario}", usuario);
                throw;
            }
        }

        public void Update(Usuario usuario, string? newPlainPassword = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                using var cmd = new NpgsqlCommand("UPDATE usuario SET nombre=@nombre, email=@email, activo=@activo WHERE id=@id", conn, tx);
                cmd.Parameters.AddWithValue("@id", usuario.Id);
                cmd.Parameters.AddWithValue("@nombre", usuario.Nombre);
                cmd.Parameters.AddWithValue("@email", usuario.Email);
                cmd.Parameters.AddWithValue("@activo", usuario.Activo);
                cmd.ExecuteNonQuery();

                if (!string.IsNullOrWhiteSpace(newPlainPassword))
                {
                    var hp = PasswordHasher.HashPassword(newPlainPassword);
                    using var up = new NpgsqlCommand("UPDATE usuario SET password_hash=@ph, password_salt=@ps WHERE id=@id", conn, tx);
                    up.Parameters.AddWithValue("@ph", hp.Hash);
                    up.Parameters.AddWithValue("@ps", hp.Salt);
                    up.Parameters.AddWithValue("@id", usuario.Id);
                    up.ExecuteNonQuery();
                }

                // Reemplaza asignaciones de roles
                using (var del = new NpgsqlCommand("DELETE FROM usuario_rol WHERE usuario_id=@uid", conn, tx))
                {
                    del.Parameters.AddWithValue("@uid", usuario.Id);
                    del.ExecuteNonQuery();
                }
                if (usuario.Roles != null && usuario.Roles.Count > 0)
                {
                    foreach (var rol in usuario.Roles)
                    {
                        using var ins = new NpgsqlCommand("INSERT INTO usuario_rol (usuario_id, rol_id) VALUES (@uid, @rid)", conn, tx);
                        ins.Parameters.AddWithValue("@uid", usuario.Id);
                        ins.Parameters.AddWithValue("@rid", rol.Id);
                        ins.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario {@Usuario}", usuario);
                throw;
            }
        }

        public void Delete(int id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                // Borra asignaciones de roles primero por FK
                using (var delUr = new NpgsqlCommand("DELETE FROM usuario_rol WHERE usuario_id=@id", conn, tx))
                {
                    delUr.Parameters.AddWithValue("@id", id);
                    delUr.ExecuteNonQuery();
                }
                using (var cmd = new NpgsqlCommand("DELETE FROM usuario WHERE id=@id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario {UsuarioId}", id);
                throw;
            }
        }

        public bool EmailExists(string email, int? excludeUserId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                var sql = "SELECT EXISTS(SELECT 1 FROM usuario WHERE lower(email) = lower(@mail)" + (excludeUserId.HasValue ? " AND id <> @id)" : ")");
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@mail", email);
                if (excludeUserId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@id", excludeUserId.Value);
                }
                var result = cmd.ExecuteScalar();
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando existencia de email {Email}", email);
                throw;
            }
        }
    }
}
