using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IPermisoRepository
    {
        List<Permiso> GetByRolIds(List<int> rolIds);
        List<Permiso> GetAll();
        List<Permiso> GetPermisosConsolidados(int usuarioId);
        List<Permiso> GetByUsuarioIdDirecto(int usuarioId);
        void AsignarPermisosDirectos(int usuarioId, List<int> permisoIds);
    }
}

