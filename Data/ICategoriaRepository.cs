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
        bool NombreExists(string nombre, long? excludeId = null);
    }
}

