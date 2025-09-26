using System.Collections.Generic;

namespace mi_ferreteria.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public bool Activo { get; set; }
        // Seguridad: no exponer en respuestas API
        public byte[]? PasswordHash { get; set; }
        public byte[]? PasswordSalt { get; set; }
        public List<Rol> Roles { get; set; } = new List<Rol>();
    }
}
