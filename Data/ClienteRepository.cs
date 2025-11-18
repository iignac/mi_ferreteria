using System;
using System.Collections.Generic;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ClienteRepository> _logger;

        public ClienteRepository(IConfiguration configuration, ILogger<ClienteRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        private void EnsureSchema(NpgsqlConnection conn)
        {
            using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
            using var cmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS cliente (
                    id BIGSERIAL PRIMARY KEY,
                    nombre TEXT NOT NULL,
                    tipo_documento TEXT NULL,
                    numero_documento TEXT NULL,
                    direccion TEXT NULL,
                    telefono TEXT NULL,
                    email TEXT NULL,
                    limite_credito NUMERIC(12,2) NOT NULL DEFAULT 0,
                    activo BOOLEAN NOT NULL DEFAULT true,
                    fecha_alta TIMESTAMPTZ NOT NULL DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS cliente_cuenta_corriente_mov (
                    id BIGSERIAL PRIMARY KEY,
                    cliente_id BIGINT NOT NULL REFERENCES cliente(id),
                    venta_id BIGINT NULL,
                    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
                    tipo TEXT NOT NULL,
                    monto NUMERIC(12,2) NOT NULL,
                    descripcion TEXT NULL,
                    usuario_id INT NULL
                );
            ", conn);
            cmd.ExecuteNonQuery();

            // Asegura columnas nuevas si la tabla ya existA-a sin estas
            using var alt = new NpgsqlCommand(@"
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS tipo_documento TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS numero_documento TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS direccion TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS telefono TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS email TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS limite_credito NUMERIC(12,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT true;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS fecha_alta TIMESTAMPTZ NOT NULL DEFAULT now();
            ", conn);
            alt.ExecuteNonQuery();
        }

        public IEnumerable<Cliente> GetAllActivos()
        {
            var list = new List<Cliente>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"SELECT id, nombre, tipo_documento, numero_documento,
                                                           direccion, telefono, email, limite_credito,
                                                           activo, fecha_alta
                                                    FROM cliente
                                                    WHERE activo = true
                                                    ORDER BY nombre ASC", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new Cliente
                    {
                        Id = reader.GetInt64(0),
                        Nombre = reader.GetString(1),
                        TipoDocumento = reader.IsDBNull(2) ? null : reader.GetString(2),
                        NumeroDocumento = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Direccion = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Telefono = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Email = reader.IsDBNull(6) ? null : reader.GetString(6),
                        LimiteCredito = reader.GetDecimal(7),
                        Activo = reader.GetBoolean(8),
                        FechaAlta = reader.GetFieldValue<DateTimeOffset>(9)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes activos");
                throw;
            }
        }

        public Cliente? GetById(long id)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"SELECT id, nombre, tipo_documento, numero_documento,
                                                           direccion, telefono, email, limite_credito,
                                                           activo, fecha_alta
                                                    FROM cliente
                                                    WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new Cliente
                    {
                        Id = reader.GetInt64(0),
                        Nombre = reader.GetString(1),
                        TipoDocumento = reader.IsDBNull(2) ? null : reader.GetString(2),
                        NumeroDocumento = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Direccion = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Telefono = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Email = reader.IsDBNull(6) ? null : reader.GetString(6),
                        LimiteCredito = reader.GetDecimal(7),
                        Activo = reader.GetBoolean(8),
                        FechaAlta = reader.GetFieldValue<DateTimeOffset>(9)
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cliente {ClienteId}", id);
                throw;
            }
        }

        public void Add(Cliente cliente)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO cliente (nombre, tipo_documento, numero_documento, direccion,
                                         telefono, email, limite_credito, activo)
                    VALUES (@nombre, @tdoc, @ndoc, @dir, @tel, @mail, @lim, @act)
                    RETURNING id, fecha_alta", conn);
                cmd.Parameters.AddWithValue("@nombre", cliente.Nombre);
                cmd.Parameters.AddWithValue("@tdoc", (object?)cliente.TipoDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ndoc", (object?)cliente.NumeroDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dir", (object?)cliente.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tel", (object?)cliente.Telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mail", (object?)cliente.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lim", cliente.LimiteCredito);
                cmd.Parameters.AddWithValue("@act", cliente.Activo);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    cliente.Id = reader.GetInt64(0);
                    cliente.FechaAlta = reader.GetFieldValue<DateTimeOffset>(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear cliente {@Cliente}", cliente);
                throw;
            }
        }

        public void Update(Cliente cliente)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    UPDATE cliente SET
                        nombre=@nombre,
                        tipo_documento=@tdoc,
                        numero_documento=@ndoc,
                        direccion=@dir,
                        telefono=@tel,
                        email=@mail,
                        limite_credito=@lim,
                        activo=@act
                    WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", cliente.Id);
                cmd.Parameters.AddWithValue("@nombre", cliente.Nombre);
                cmd.Parameters.AddWithValue("@tdoc", (object?)cliente.TipoDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ndoc", (object?)cliente.NumeroDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dir", (object?)cliente.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tel", (object?)cliente.Telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mail", (object?)cliente.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lim", cliente.LimiteCredito);
                cmd.Parameters.AddWithValue("@act", cliente.Activo);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar cliente {@Cliente}", cliente);
                throw;
            }
        }

        public decimal GetSaldoCuentaCorriente(long clienteId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    SELECT COALESCE(SUM(
                        CASE
                            WHEN tipo = 'DEUDA' THEN monto
                            WHEN tipo = 'PAGO' THEN -monto
                            WHEN tipo = 'AJUSTE' THEN monto
                            ELSE 0
                        END
                    ), 0)
                    FROM cliente_cuenta_corriente_mov
                    WHERE cliente_id=@cid", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                var obj = cmd.ExecuteScalar();
                return obj is decimal d ? d : Convert.ToDecimal(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener saldo de cuenta corriente del cliente {ClienteId}", clienteId);
                throw;
            }
        }

        public void RegistrarDeuda(long clienteId, long ventaId, decimal monto, int usuarioId, string descripcion)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO cliente_cuenta_corriente_mov
                        (cliente_id, venta_id, tipo, monto, descripcion, usuario_id)
                    VALUES (@cid, @vid, 'DEUDA', @monto, @desc, @uid)", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                cmd.Parameters.AddWithValue("@vid", ventaId);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar deuda para cliente {ClienteId} por venta {VentaId}", clienteId, ventaId);
                throw;
            }
        }
    }
}
