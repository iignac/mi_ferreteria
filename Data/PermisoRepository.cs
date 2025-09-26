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
    }
}
