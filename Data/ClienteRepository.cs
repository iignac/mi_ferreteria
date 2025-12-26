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
        private const int DiasVencimientoCuentaCorriente = 30;
        private readonly string _connectionString;
        private readonly ILogger<ClienteRepository> _logger;

        public ClienteRepository(IConfiguration configuration, ILogger<ClienteRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgresConnection");
            _logger = logger;
        }

        public int Count(string? q = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                string sql = @"
                    SELECT COUNT(1)
                    FROM cliente
                    WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(q))
                {
                    sql += " AND (unaccent(lower(nombre)) LIKE unaccent(lower(@q))" +
                           " OR unaccent(lower(coalesce(apellido,''))) LIKE unaccent(lower(@q))" +
                           " OR lower(coalesce(numero_documento,'')) LIKE lower(@q)" +
                           " OR lower(coalesce(email,'')) LIKE lower(@q))";
                }
                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    cmd.Parameters.AddWithValue("@q", "%" + q.Trim() + "%");
                }
                var obj = cmd.ExecuteScalar();
                return obj is long l ? (int)l : Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar clientes (q={Query})", q);
                throw;
            }
        }

        public IEnumerable<Cliente> GetPage(string? q, int page, int pageSize)
        {
            var list = new List<Cliente>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;
                string sql = @"
                    SELECT id, nombre, apellido, tipo_documento, numero_documento,
                           direccion, telefono, email, tipo_cliente, cuenta_corriente_habilitada,
                           limite_credito, activo, fecha_alta
                    FROM cliente
                    WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(q))
                {
                    sql += " AND (unaccent(lower(nombre)) LIKE unaccent(lower(@q))" +
                           " OR unaccent(lower(coalesce(apellido,''))) LIKE unaccent(lower(@q))" +
                           " OR lower(coalesce(numero_documento,'')) LIKE lower(@q)" +
                           " OR lower(coalesce(email,'')) LIKE lower(@q))";
                }
                sql += " ORDER BY nombre ASC, apellido ASC NULLS LAST, id ASC LIMIT @limit OFFSET @offset";
                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    cmd.Parameters.AddWithValue("@q", "%" + q.Trim() + "%");
                }
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new Cliente
                    {
                        Id = reader.GetInt64(0),
                        Nombre = reader.GetString(1),
                        Apellido = reader.IsDBNull(2) ? null : reader.GetString(2),
                        TipoDocumento = reader.IsDBNull(3) ? null : reader.GetString(3),
                        NumeroDocumento = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Direccion = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Telefono = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Email = reader.IsDBNull(7) ? null : reader.GetString(7),
                        TipoCliente = reader.IsDBNull(8) ? "CONSUMIDOR_FINAL" : reader.GetString(8),
                        CuentaCorrienteHabilitada = !reader.IsDBNull(9) && reader.GetBoolean(9),
                        LimiteCredito = reader.GetDecimal(10),
                        Activo = reader.GetBoolean(11),
                        FechaAlta = reader.GetFieldValue<DateTimeOffset>(12)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pagina de clientes (q={Query}, page={Page})", q, page);
                throw;
            }
        }

        private void EnsureSchema(NpgsqlConnection conn)
        {
            using (var set = new NpgsqlCommand("SET search_path TO venta, public", conn)) { set.ExecuteNonQuery(); }
            using var cmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS cliente (
                    id BIGSERIAL PRIMARY KEY,
                    nombre TEXT NOT NULL,
                    apellido TEXT NULL,
                    tipo_documento TEXT NULL,
                    numero_documento TEXT NULL,
                    direccion TEXT NULL,
                    telefono TEXT NULL,
                    email TEXT NULL,
                    tipo_cliente TEXT NOT NULL DEFAULT 'CONSUMIDOR_FINAL',
                    cuenta_corriente_habilitada BOOLEAN NOT NULL DEFAULT false,
                    limite_credito NUMERIC(12,2) NOT NULL DEFAULT 0,
                    activo BOOLEAN NOT NULL DEFAULT true,
                    fecha_alta TIMESTAMPTZ NOT NULL DEFAULT now()
                );

                CREATE TABLE IF NOT EXISTS cliente_cuenta_corriente_mov (
                    id BIGSERIAL PRIMARY KEY,
                    cliente_id BIGINT NOT NULL REFERENCES cliente(id),
                    venta_id BIGINT NULL,
                    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
                    fecha_vencimiento TIMESTAMPTZ NULL,
                    tipo TEXT NOT NULL,
                    monto NUMERIC(12,2) NOT NULL,
                    descripcion TEXT NULL,
                    usuario_id INT NULL,
                    movimiento_relacionado_id BIGINT NULL REFERENCES cliente_cuenta_corriente_mov(id)
                );
            ", conn);
            cmd.ExecuteNonQuery();

            // Asegura columnas nuevas si la tabla ya existA-a sin estas
            using var alt = new NpgsqlCommand(@"
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS apellido TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS tipo_documento TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS numero_documento TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS direccion TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS telefono TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS email TEXT NULL;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS tipo_cliente TEXT NOT NULL DEFAULT 'CONSUMIDOR_FINAL';
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS cuenta_corriente_habilitada BOOLEAN NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS limite_credito NUMERIC(12,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT true;
                ALTER TABLE IF EXISTS cliente ADD COLUMN IF NOT EXISTS fecha_alta TIMESTAMPTZ NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS cliente_cuenta_corriente_mov ADD COLUMN IF NOT EXISTS fecha_vencimiento TIMESTAMPTZ NULL;
                ALTER TABLE IF EXISTS cliente_cuenta_corriente_mov ADD COLUMN IF NOT EXISTS movimiento_relacionado_id BIGINT NULL;
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
                using var cmd = new NpgsqlCommand(@"SELECT id, nombre, apellido, tipo_documento, numero_documento,
                                                           direccion, telefono, email, tipo_cliente, cuenta_corriente_habilitada,
                                                           limite_credito, activo, fecha_alta
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
                        Apellido = reader.IsDBNull(2) ? null : reader.GetString(2),
                        TipoDocumento = reader.IsDBNull(3) ? null : reader.GetString(3),
                        NumeroDocumento = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Direccion = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Telefono = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Email = reader.IsDBNull(7) ? null : reader.GetString(7),
                        TipoCliente = reader.IsDBNull(8) ? "CONSUMIDOR_FINAL" : reader.GetString(8),
                        CuentaCorrienteHabilitada = !reader.IsDBNull(9) && reader.GetBoolean(9),
                        LimiteCredito = reader.GetDecimal(10),
                        Activo = reader.GetBoolean(11),
                        FechaAlta = reader.GetFieldValue<DateTimeOffset>(12)
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
                using var cmd = new NpgsqlCommand(@"SELECT id, nombre, apellido, tipo_documento, numero_documento,
                                                           direccion, telefono, email, tipo_cliente, cuenta_corriente_habilitada,
                                                           limite_credito, activo, fecha_alta
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
                        Apellido = reader.IsDBNull(2) ? null : reader.GetString(2),
                        TipoDocumento = reader.IsDBNull(3) ? null : reader.GetString(3),
                        NumeroDocumento = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Direccion = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Telefono = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Email = reader.IsDBNull(7) ? null : reader.GetString(7),
                        TipoCliente = reader.IsDBNull(8) ? "CONSUMIDOR_FINAL" : reader.GetString(8),
                        CuentaCorrienteHabilitada = !reader.IsDBNull(9) && reader.GetBoolean(9),
                        LimiteCredito = reader.GetDecimal(10),
                        Activo = reader.GetBoolean(11),
                        FechaAlta = reader.GetFieldValue<DateTimeOffset>(12)
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
                    INSERT INTO cliente (nombre, apellido, tipo_documento, numero_documento, direccion,
                                         telefono, email, tipo_cliente, cuenta_corriente_habilitada, limite_credito, activo)
                    VALUES (@nombre, @ape, @tdoc, @ndoc, @dir, @tel, @mail, @tcli, @cc_hab, @lim, @act)
                    RETURNING id, fecha_alta", conn);
                cmd.Parameters.AddWithValue("@nombre", cliente.Nombre);
                cmd.Parameters.AddWithValue("@ape", (object?)cliente.Apellido ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tdoc", (object?)cliente.TipoDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ndoc", (object?)cliente.NumeroDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dir", (object?)cliente.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tel", (object?)cliente.Telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mail", (object?)cliente.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tcli", cliente.TipoCliente ?? "CONSUMIDOR_FINAL");
                cmd.Parameters.AddWithValue("@cc_hab", cliente.CuentaCorrienteHabilitada);
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
                        apellido=@ape,
                        tipo_documento=@tdoc,
                        numero_documento=@ndoc,
                        direccion=@dir,
                        telefono=@tel,
                        email=@mail,
                        tipo_cliente=@tcli,
                        cuenta_corriente_habilitada=@cc_hab,
                        limite_credito=@lim,
                        activo=@act
                    WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", cliente.Id);
                cmd.Parameters.AddWithValue("@nombre", cliente.Nombre);
                cmd.Parameters.AddWithValue("@ape", (object?)cliente.Apellido ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tdoc", (object?)cliente.TipoDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ndoc", (object?)cliente.NumeroDocumento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dir", (object?)cliente.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tel", (object?)cliente.Telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mail", (object?)cliente.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tcli", cliente.TipoCliente ?? "CONSUMIDOR_FINAL");
                cmd.Parameters.AddWithValue("@cc_hab", cliente.CuentaCorrienteHabilitada);
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
                            WHEN tipo IN ('DEUDA', 'NOTA_DEBITO') THEN -monto
                            WHEN tipo IN ('PAGO', 'NOTA_CREDITO') THEN monto
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

        public long RegistrarDeuda(long clienteId, long ventaId, decimal monto, int usuarioId, string descripcion, DateTimeOffset? fechaVencimiento = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                var vencimiento = fechaVencimiento?.ToUniversalTime() ?? DateTimeOffset.UtcNow.AddDays(DiasVencimientoCuentaCorriente);
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO cliente_cuenta_corriente_mov
                        (cliente_id, venta_id, fecha_vencimiento, tipo, monto, descripcion, usuario_id)
                    VALUES (@cid, @vid, @fv, 'DEUDA', @monto, @desc, @uid)
                    RETURNING id", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                cmd.Parameters.AddWithValue("@vid", ventaId);
                cmd.Parameters.AddWithValue("@fv", (object?)vencimiento.UtcDateTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                var res = cmd.ExecuteScalar();
                return res is long l ? l : Convert.ToInt64(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar deuda para cliente {ClienteId} por venta {VentaId}", clienteId, ventaId);
                throw;
            }
        }

        public long RegistrarNotaDebito(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId, long? movimientoRelacionadoId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO cliente_cuenta_corriente_mov
                        (cliente_id, venta_id, movimiento_relacionado_id, tipo, monto, descripcion, usuario_id)
                    VALUES (@cid, @vid, @rel, 'NOTA_DEBITO', @monto, @desc, @uid)
                    RETURNING id", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                cmd.Parameters.AddWithValue("@vid", (object?)ventaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rel", (object?)movimientoRelacionadoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                var res = cmd.ExecuteScalar();
                return res is long l ? l : Convert.ToInt64(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar nota de dИbito para cliente {ClienteId}", clienteId);
                throw;
            }
        }

        public long RegistrarNotaCredito(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId = null, long? movimientoRelacionadoId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO cliente_cuenta_corriente_mov
                        (cliente_id, venta_id, movimiento_relacionado_id, tipo, monto, descripcion, usuario_id)
                    VALUES (@cid, @vid, @rel, 'NOTA_CREDITO', @monto, @desc, @uid)
                    RETURNING id", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                cmd.Parameters.AddWithValue("@vid", (object?)ventaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rel", (object?)movimientoRelacionadoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                var res = cmd.ExecuteScalar();
                return res is long l ? l : Convert.ToInt64(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar nota de credito para cliente {ClienteId}", clienteId);
                throw;
            }
        }

        public ClienteCuentaCorrienteMovimiento? GetMovimiento(long movimientoId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    SELECT id, cliente_id, venta_id, fecha, fecha_vencimiento, tipo, monto, descripcion, usuario_id, movimiento_relacionado_id
                    FROM cliente_cuenta_corriente_mov
                    WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", movimientoId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new ClienteCuentaCorrienteMovimiento
                    {
                        Id = reader.GetInt64(0),
                        ClienteId = reader.GetInt64(1),
                        VentaId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                        Fecha = reader.GetFieldValue<DateTimeOffset>(3),
                        FechaVencimiento = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                        Tipo = reader.GetString(5),
                        Monto = reader.GetDecimal(6),
                        Descripcion = reader.IsDBNull(7) ? null : reader.GetString(7),
                        UsuarioId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        MovimientoRelacionadoId = reader.IsDBNull(9) ? null : reader.GetInt64(9)
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimiento {MovimientoId}", movimientoId);
                throw;
            }
        }

        public IEnumerable<ClienteCuentaCorrienteMovimiento> GetMovimientosCuentaCorriente(long clienteId)
        {
            var list = new List<ClienteCuentaCorrienteMovimiento>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    WITH base AS (
                        SELECT ccm.id, ccm.cliente_id, ccm.venta_id, ccm.fecha, ccm.fecha_vencimiento,
                               ccm.tipo, ccm.monto, ccm.descripcion, ccm.usuario_id, ccm.movimiento_relacionado_id,
                               f.tipo_comprobante, f.punto_venta, f.numero, f.fecha_emision
                        FROM cliente_cuenta_corriente_mov ccm
                        LEFT JOIN factura f ON f.venta_id = ccm.venta_id
                        WHERE ccm.cliente_id = @cid
                    )
                    SELECT id, cliente_id, venta_id, fecha, fecha_vencimiento, tipo, monto, descripcion,
                           usuario_id, movimiento_relacionado_id,
                           tipo_comprobante, punto_venta, numero, fecha_emision,
                           SUM(
                                CASE
                                    WHEN tipo IN ('DEUDA','NOTA_DEBITO') THEN -monto
                                    WHEN tipo IN ('PAGO','NOTA_CREDITO') THEN monto
                                    WHEN tipo = 'AJUSTE' THEN monto
                                    ELSE 0
                                END
                           ) OVER (ORDER BY fecha ASC, id ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS saldo
                    FROM base
                    ORDER BY fecha ASC, id ASC;", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string? comprobante = null;
                    if (!reader.IsDBNull(10) && !reader.IsDBNull(11) && !reader.IsDBNull(12))
                    {
                        var tipo = reader.GetString(10);
                        var pv = reader.GetInt32(11);
                        var numero = reader.GetInt64(12);
                        comprobante = $"{tipo} {pv:0000}-{numero:00000000}";
                    }
                    list.Add(new ClienteCuentaCorrienteMovimiento
                    {
                        Id = reader.GetInt64(0),
                        ClienteId = reader.GetInt64(1),
                        VentaId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                        Fecha = reader.GetFieldValue<DateTimeOffset>(3),
                        FechaVencimiento = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                        Tipo = reader.GetString(5),
                        Monto = reader.GetDecimal(6),
                        Descripcion = reader.IsDBNull(7) ? null : reader.GetString(7),
                        UsuarioId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        MovimientoRelacionadoId = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                        Comprobante = comprobante,
                        ComprobanteFecha = reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
                        SaldoAcumulado = reader.GetDecimal(14)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener movimientos de cuenta corriente del cliente {ClienteId}", clienteId);
                throw;
            }
        }

        public long RegistrarPagoCuentaCorriente(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId = null, long? movimientoRelacionadoId = null)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO cliente_cuenta_corriente_mov
                        (cliente_id, venta_id, movimiento_relacionado_id, tipo, monto, descripcion, usuario_id)
                    VALUES (@cid, @vid, @rel, 'PAGO', @monto, @desc, @uid)
                    RETURNING id", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                cmd.Parameters.AddWithValue("@vid", (object?)ventaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rel", (object?)movimientoRelacionadoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                var res = cmd.ExecuteScalar();
                return res is long l ? l : Convert.ToInt64(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar pago en cuenta corriente para cliente {ClienteId}", clienteId);
                throw;
            }
        }

        public IEnumerable<ClienteCuentaCorrienteFacturaPendiente> GetFacturasPendientes(long clienteId)
        {
            var list = new List<ClienteCuentaCorrienteFacturaPendiente>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                EnsureSchema(conn);
                using var cmd = new NpgsqlCommand(@"
                    WITH base AS (
                        SELECT ccm.id, ccm.venta_id, ccm.fecha, ccm.fecha_vencimiento, ccm.tipo, ccm.monto,
                               f.tipo_comprobante, f.punto_venta, f.numero, f.fecha_emision
                        FROM cliente_cuenta_corriente_mov ccm
                        LEFT JOIN factura f ON f.venta_id = ccm.venta_id
                        WHERE ccm.cliente_id = @cid AND ccm.venta_id IS NOT NULL
                    ),
                    agrupado AS (
                        SELECT
                            venta_id,
                            MAX(id) FILTER (WHERE tipo = 'DEUDA') AS movimiento_deuda_id,
                            MAX(fecha) FILTER (WHERE tipo = 'DEUDA') AS fecha_deuda,
                            MAX(fecha_vencimiento) AS fecha_vencimiento_original,
                            MAX(monto) FILTER (WHERE tipo = 'DEUDA') AS importe_deuda,
                            MAX(tipo_comprobante) AS tipo_comprobante,
                            MAX(punto_venta) AS punto_venta,
                            MAX(numero) AS numero,
                            MAX(fecha_emision) AS fecha_emision,
                            SUM(
                                CASE
                                    WHEN tipo IN ('DEUDA','NOTA_DEBITO') THEN -monto
                                    WHEN tipo IN ('PAGO','NOTA_CREDITO') THEN monto
                                    WHEN tipo = 'AJUSTE' THEN monto
                                    ELSE 0
                                END
                            ) AS saldo_pendiente
                        FROM base
                        GROUP BY venta_id
                    )
                    SELECT movimiento_deuda_id,
                           venta_id,
                           COALESCE(fecha_emision, fecha_deuda) AS fecha_emision,
                           COALESCE(fecha_vencimiento_original, fecha_deuda + (@dias::text || ' days')::interval) AS fecha_vencimiento,
                           COALESCE(importe_deuda, 0) AS importe_deuda,
                           -saldo_pendiente AS saldo_pendiente,
                           tipo_comprobante,
                           punto_venta,
                           numero
                    FROM agrupado
                    WHERE saldo_pendiente < 0
                    ORDER BY fecha_vencimiento ASC;", conn);
                cmd.Parameters.AddWithValue("@cid", clienteId);
                cmd.Parameters.AddWithValue("@dias", DiasVencimientoCuentaCorriente);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string? comprobante = null;
                    if (!reader.IsDBNull(6) && !reader.IsDBNull(7) && !reader.IsDBNull(8))
                    {
                        comprobante = $"{reader.GetString(6)} {reader.GetInt32(7):0000}-{reader.GetInt64(8):00000000}";
                    }
                    list.Add(new ClienteCuentaCorrienteFacturaPendiente
                    {
                        MovimientoDeudaId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                        VentaId = reader.GetInt64(1),
                        FechaEmision = reader.IsDBNull(2) ? DateTimeOffset.UtcNow : reader.GetFieldValue<DateTimeOffset>(2),
                        FechaVencimiento = reader.GetFieldValue<DateTimeOffset>(3),
                        ImporteOriginal = reader.GetDecimal(4),
                        SaldoPendiente = reader.GetDecimal(5),
                        Comprobante = comprobante
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener facturas vencidas del cliente {ClienteId}", clienteId);
                throw;
            }
        }
    }
}
