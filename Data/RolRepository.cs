using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using mi_ferreteria.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace mi_ferreteria.Data
{
    public class RolRepository : IRolRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<RolRepository> _logger;

        public RolRepository(IConfiguration configuration, ILogger<RolRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        public List<Rol> GetAll()
        {
            var roles = new List<Rol>();
            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener roles");
                throw;
            }
        }
    }
}
