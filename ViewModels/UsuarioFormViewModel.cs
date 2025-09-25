using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class UsuarioFormViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public bool Activo { get; set; }
        public List<int> RolesIds { get; set; } = new List<int>();
    public List<Rol> RolesDisponibles { get; set; } = new List<Rol>();
    public List<Permiso> PermisosHeredados { get; set; } = new List<Permiso>();
    }
}
