using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.ViewModels
{
    public class ClienteCreateViewModel
    {
        public long? Id { get; set; }

        [Required(ErrorMessage = "El nombre o razón social es obligatorio.")]
        [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
        [RegularExpression(@".*\S.*", ErrorMessage = "El nombre o razón social es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "El apellido no puede superar los 120 caracteres.")]
        public string? Apellido { get; set; }

        [Display(Name = "Tipo de documento")]
        public string? TipoDocumento { get; set; }

        [Display(Name = "Número de documento / CUIT")]
        [StringLength(20, ErrorMessage = "El documento no puede superar los 20 caracteres.")]
        public string? NumeroDocumento { get; set; }

        [Display(Name = "Calle")]
        [StringLength(120, ErrorMessage = "La calle no puede superar los 120 caracteres.")]
        public string? DireccionCalle { get; set; }

        [Display(Name = "Número")]
        [StringLength(10, ErrorMessage = "El número no puede superar los 10 caracteres.")]
        public string? DireccionNumero { get; set; }

        [Display(Name = "Localidad")]
        [StringLength(120, ErrorMessage = "La localidad no puede superar los 120 caracteres.")]
        public string? DireccionLocalidad { get; set; }

        [Display(Name = "Teléfono")]
        [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un tipo de cliente.")]
        [RegularExpression("^(CONSUMIDOR_FINAL|CUENTA_CORRIENTE)$", ErrorMessage = "Tipo de cliente inválido.")]
        public string TipoCliente { get; set; } = "CONSUMIDOR_FINAL";

        [Display(Name = "Habilitar cuenta corriente")]
        public bool CuentaCorrienteHabilitada { get; set; }

        [Display(Name = "Límite de crédito")]
        public decimal LimiteCredito { get; set; }

        [Display(Name = "Saldo inicial cuenta corriente")]
        [Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "El saldo inicial debe estar entre -999.999.999 y 999.999.999.")]
        public decimal SaldoInicialCuentaCorriente { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;
    }
}

