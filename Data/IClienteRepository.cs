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
        long RegistrarNotaDebito(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId, long? movimientoRelacionadoId = null);
        long RegistrarNotaCredito(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId = null, long? movimientoRelacionadoId = null);
        long RegistrarPagoCuentaCorriente(long clienteId, decimal monto, int usuarioId, string descripcion, long? ventaId = null, long? movimientoRelacionadoId = null);
        ClienteCuentaCorrienteMovimiento? GetMovimiento(long movimientoId);
        IEnumerable<ClienteCuentaCorrienteMovimiento> GetMovimientosCuentaCorriente(long clienteId);
        IEnumerable<ClienteCuentaCorrienteFacturaPendiente> GetFacturasPendientes(long clienteId);

        // Listado paginado y bA-squeda
        int Count(string? q = null);
        IEnumerable<Cliente> GetPage(string? q, int page, int pageSize);
    }
}
