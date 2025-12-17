using System;
using System.Collections.Generic;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public class AuditoriaRepository : IAuditoriaRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AuditoriaRepository> _logger;

        public AuditoriaRepository(IConfiguration configuration, ILogger<AuditoriaRepository> logger)
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
                CREATE TABLE IF NOT EXISTS auditoria_usuario (
                    id BIGSERIAL PRIMARY KEY,
                    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
                    usuario_id INT NOT NULL,
                    usuario_nombre TEXT NOT NULL,
                    accion TEXT NOT NULL,
                    detalle TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_auditoria_usuario_fecha ON auditoria_usuario (fecha DESC);
            ", conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void Registrar(int usuarioId, string usuarioNombre, string accion, string? detalle = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO auditoria_usuario (usuario_id, usuario_nombre, accion, detalle) VALUES (@uid, @unom, @acc, @det)",
                    conn);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cmd.Parameters.AddWithValue("@unom", usuarioNombre);
                cmd.Parameters.AddWithValue("@acc", accion);
                cmd.Parameters.AddWithValue("@det", (object?)detalle ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar auditoria de {UsuarioId} accion {Accion}", usuarioId, accion);
            }
        }

        public (IEnumerable<AuditoriaRegistro> Registros, int Total) GetPage(int page, int pageSize)
        {
            var list = new List<AuditoriaRegistro>();
            if (page < 1) page = 1;
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);

                int total;
                using (var count = new NpgsqlCommand("SELECT COUNT(1) FROM auditoria_usuario", conn))
                {
                    var res = count.ExecuteScalar();
                    total = res is long l ? (int)l : Convert.ToInt32(res);
                }

                var offset = (page - 1) * pageSize;
                using (var cmd = new NpgsqlCommand(@"
                    SELECT id, fecha, usuario_id, usuario_nombre, accion, detalle
                    FROM auditoria_usuario
                    ORDER BY fecha DESC, id DESC
                    LIMIT @limit OFFSET @offset", conn))
                {
                    cmd.Parameters.AddWithValue("@limit", pageSize);
                    cmd.Parameters.AddWithValue("@offset", offset);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        list.Add(new AuditoriaRegistro
                        {
                            Id = r.GetInt64(0),
                            Fecha = r.GetFieldValue<DateTimeOffset>(1),
                            UsuarioId = r.GetInt32(2),
                            UsuarioNombre = r.GetString(3),
                            Accion = r.GetString(4),
                            Detalle = r.IsDBNull(5) ? null : r.GetString(5)
                        });
                    }
                }

                return (list, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener auditoria (page={Page})", page);
                throw;
            }
        }
    }
}
