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

        public (IEnumerable<AuditoriaRegistro> Registros, int Total) GetPage(
            int page,
            int pageSize,
            string? accionFiltro = null,
            string? modulo = null,
            string? operacion = null,
            DateTimeOffset? fechaDesde = null,
            DateTimeOffset? fechaHasta = null)
        {
            var list = new List<AuditoriaRegistro>();
            if (page < 1) page = 1;
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);

                var filtro = string.IsNullOrWhiteSpace(accionFiltro) ? null : accionFiltro.Trim();
                var filtroParametro = filtro == null ? null : $"%{filtro}%";
                var tieneFiltro = filtro != null;
                var moduloFiltro = string.IsNullOrWhiteSpace(modulo) ? null : modulo.Trim().ToUpperInvariant();
                var operacionFiltro = string.IsNullOrWhiteSpace(operacion) ? null : operacion.Trim().ToUpperInvariant();
                var desdeFiltro = fechaDesde;
                var hastaFiltro = fechaHasta;

                var condiciones = new List<string>();
                if (tieneFiltro)
                {
                    condiciones.Add("accion ILIKE @accionFiltro");
                }
                if (moduloFiltro != null)
                {
                    condiciones.Add("accion LIKE @moduloFiltro");
                }
                if (operacionFiltro != null)
                {
                    condiciones.Add("accion LIKE @operacionFiltro");
                }
                if (desdeFiltro.HasValue)
                {
                    condiciones.Add("fecha >= @fechaDesde");
                }
                if (hastaFiltro.HasValue)
                {
                    condiciones.Add("fecha < @fechaHasta");
                }

                var whereSql = condiciones.Count > 0 ? " WHERE " + string.Join(" AND ", condiciones) : string.Empty;

                int total;
                var countSql = "SELECT COUNT(1) FROM auditoria_usuario" + whereSql;
                using (var count = new NpgsqlCommand(countSql, conn))
                {
                    if (tieneFiltro)
                    {
                        count.Parameters.AddWithValue("@accionFiltro", filtroParametro!);
                    }
                    if (moduloFiltro != null)
                    {
                        count.Parameters.AddWithValue("@moduloFiltro", $"{moduloFiltro}.%");
                    }
                    if (operacionFiltro != null)
                    {
                        count.Parameters.AddWithValue("@operacionFiltro", $"%.{operacionFiltro}");
                    }
                    if (desdeFiltro.HasValue)
                    {
                        count.Parameters.AddWithValue("@fechaDesde", desdeFiltro.Value.UtcDateTime);
                    }
                    if (hastaFiltro.HasValue)
                    {
                        count.Parameters.AddWithValue("@fechaHasta", hastaFiltro.Value.UtcDateTime);
                    }
                    var res = count.ExecuteScalar();
                    total = res is long l ? (int)l : Convert.ToInt32(res);
                }

                var offset = (page - 1) * pageSize;
                var query = @"
                    SELECT id, fecha, usuario_id, usuario_nombre, accion, detalle
                    FROM auditoria_usuario" + whereSql + @"
                    ORDER BY fecha DESC, id DESC
                    LIMIT @limit OFFSET @offset";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@limit", pageSize);
                    cmd.Parameters.AddWithValue("@offset", offset);
                    if (tieneFiltro)
                    {
                        cmd.Parameters.AddWithValue("@accionFiltro", filtroParametro!);
                    }
                    if (moduloFiltro != null)
                    {
                        cmd.Parameters.AddWithValue("@moduloFiltro", $"{moduloFiltro}.%");
                    }
                    if (operacionFiltro != null)
                    {
                        cmd.Parameters.AddWithValue("@operacionFiltro", $"%.{operacionFiltro}");
                    }
                    if (desdeFiltro.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@fechaDesde", desdeFiltro.Value.UtcDateTime);
                    }
                    if (hastaFiltro.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@fechaHasta", hastaFiltro.Value.UtcDateTime);
                    }
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
