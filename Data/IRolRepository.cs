using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IRolRepository
    {
        List<Rol> GetAll();
    }
}

