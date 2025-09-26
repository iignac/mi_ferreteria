using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IUsuarioRepository
    {
        List<Usuario> GetAll();
        void Add(Usuario usuario, string plainPassword);
        void Update(Usuario usuario, string? newPlainPassword = null);
        void Delete(int id);
        bool EmailExists(string email, int? excludeUserId = null);
    }
}
