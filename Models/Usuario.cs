using System.Collections.Generic;

namespace mi_ferreteria.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public bool Activo { get; set; }
        public List<Rol> Roles { get; set; } = new List<Rol>();
    }
}
