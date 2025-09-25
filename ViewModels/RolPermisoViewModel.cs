using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class RolPermisoViewModel
    {
        public int RolId { get; set; }
        public string RolNombre { get; set; }
        public List<int> PermisosIds { get; set; } = new List<int>();
        public List<Permiso> PermisosDisponibles { get; set; } = new List<Permiso>();
    }
}
