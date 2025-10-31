using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface ICategoriaRepository
    {
        IEnumerable<Categoria> GetAll();
        Categoria? GetById(long id);
        void Add(Categoria categoria);
        void Update(Categoria categoria);
        void Delete(long id);
        void Activate(long id);
        void HardDelete(long id);
        bool NombreExists(string nombre, long? excludeId = null);
        // Paginación
        int CountAll();
        int CountSearch(string query);
        IEnumerable<Categoria> GetPage(int page, int pageSize);
        IEnumerable<Categoria> GetPageSorted(int page, int pageSize, string sort);
        IEnumerable<Categoria> SearchPageSorted(string query, int page, int pageSize, string sort);
    }
}

