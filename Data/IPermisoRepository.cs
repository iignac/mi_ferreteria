using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IPermisoRepository
    {
        List<Permiso> GetByRolIds(List<int> rolIds);
    }
}

