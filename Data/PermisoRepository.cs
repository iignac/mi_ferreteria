using System.Collections.Generic;
using System.Data;
using Npgsql;
using mi_ferreteria.Models;
using Microsoft.Extensions.Configuration;

namespace mi_ferreteria.Data
{
    public class PermisoRepository
    {
        private readonly string _connectionString;

        public PermisoRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
        }

        public List<Permiso> GetByRolIds(List<int> rolIds)
        {
            var permisos = new List<Permiso>();
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
    }
}
