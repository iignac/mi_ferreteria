using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IClienteRepository
    {
        IEnumerable<Cliente> GetAllActivos();
        Cliente? GetById(long id);
        void Add(Cliente cliente);
        void Update(Cliente cliente);

        decimal GetSaldoCuentaCorriente(long clienteId);
        long RegistrarDeuda(long clienteId, long ventaId, decimal monto, int usuarioId, string descripcion, DateTimeOffset? fechaVencimiento = null);
        long RegistrarConsumoSaldo(long clienteId, long ventaId, decimal monto, int usuarioId, string descripcion);
        long RegistrarNotaDebito(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId, long? movimientoRelacionadoId = null);
        long RegistrarNotaCredito(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId = null, long? movimientoRelacionadoId = null);
        long RegistrarPagoCuentaCorriente(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId = null, long? movimientoRelacionadoId = null);
        ClienteCuentaCorrienteMovimiento? GetMovimiento(long movimientoId);
        IEnumerable<ClienteCuentaCorrienteMovimiento> GetMovimientosCuentaCorriente(long clienteId, int page, int pageSize, long? ventaId = null);
        int CountMovimientosCuentaCorriente(long clienteId, long? ventaId = null);
        IEnumerable<ClienteCuentaCorrienteFacturaPendiente> GetFacturasPendientes(long clienteId);

        void RegistrarSaldoInicial(long clienteId, decimal monto);

        // Listado paginado y búsqueda
        int Count(string? q = null);
        IEnumerable<Cliente> GetPage(string? q, int page, int pageSize, string sort);
    }
}
