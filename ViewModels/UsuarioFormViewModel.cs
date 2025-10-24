using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class UsuarioFormViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre debe tener hasta 100 caracteres")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido")]
        [StringLength(150, ErrorMessage = "El email debe tener hasta 150 caracteres")]
        public string Email { get; set; }
        public bool Activo { get; set; }
        // Para creación y cambio de contraseña
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string? ConfirmPassword { get; set; }
        public List<int> RolesIds { get; set; } = new List<int>();
    public List<Rol> RolesDisponibles { get; set; } = new List<Rol>();
    public List<Permiso> PermisosHeredados { get; set; } = new List<Permiso>();
        // Concurrency token (optimistic lock): hash de los datos cargados en GET
        public string? OriginalHash { get; set; }
    }
}
