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
        void RegistrarDeuda(long clienteId, long ventaId, decimal monto, int usuarioId, string descripcion);

        // Listado paginado y bA-squeda
        int Count(string? q = null);
        IEnumerable<Cliente> GetPage(string? q, int page, int pageSize);
    }
}
