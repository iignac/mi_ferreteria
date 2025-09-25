using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class UsuarioRolViewModel
    {
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public List<int> RolesIds { get; set; } = new List<int>();
        public List<Rol> RolesDisponibles { get; set; } = new List<Rol>();
    }
}
