using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using mi_ferreteria.Helpers;
using mi_ferreteria.Models;
using mi_ferreteria.Security;

namespace mi_ferreteria.ViewModels
{
    public class UsuarioFormViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(120, ErrorMessage = "El nombre debe tener hasta 120 caracteres")]
        [RegularExpression(ValidationConstants.NombreSoloLetrasPattern, ErrorMessage = "El nombre solo puede contener letras, espacios, apostrofes o guiones.")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido")]
        [StringLength(120, ErrorMessage = "El email debe tener hasta 120 caracteres")]
        public string Email { get; set; }
        public bool Activo { get; set; }
        // Para creación y cambio de contraseña
        [PasswordPolicyValidation(AllowEmpty = true)]
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string? ConfirmPassword { get; set; }
        public List<int> RolesIds { get; set; } = new List<int>();
        public List<Rol> RolesDisponibles { get; set; } = new List<Rol>();
        public List<int> PermisosIds { get; set; } = new List<int>();
        public List<Permiso> TodosLosPermisos { get; set; } = new List<Permiso>();
        public List<string> PermisosHeredados { get; set; } = new List<string>();
        public Dictionary<int, List<int>> PermisosPorRol { get; set; } = new Dictionary<int, List<int>>();
        // Concurrency token (optimistic lock): hash de los datos cargados en GET
        public string? OriginalHash { get; set; }
    }
}
