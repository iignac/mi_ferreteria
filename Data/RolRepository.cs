using System.Collections.Generic;
using System.Data;
using Npgsql;
using mi_ferreteria.Models;
using Microsoft.Extensions.Configuration;

namespace mi_ferreteria.Data
{
    public class RolRepository
    {
        private readonly string _connectionString;

        public RolRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
        }

        public List<Rol> GetAll()
        {
            var roles = new List<Rol>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT id, nombre, descripcion FROM rol", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                roles.Add(new Rol
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Descripcion = reader.GetString(2)
                });
            }
            return roles;
        }
    }
}
