using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.Dtos
{
    public class UsuarioCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        public bool Activo { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        [Required]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; }
        public List<int> RolesIds { get; set; } = new List<int>();
    }

    public class UsuarioUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        public bool Activo { get; set; }

        // Para update la contraseña es opcional
        [MinLength(6)]
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string? ConfirmPassword { get; set; }

        public List<int> RolesIds { get; set; } = new List<int>();
    }

    public class RolSimpleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
    }

    public class UsuarioResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public bool Activo { get; set; }
        public List<RolSimpleDto> Roles { get; set; } = new List<RolSimpleDto>();
    }
}
