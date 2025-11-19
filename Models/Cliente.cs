using System;

namespace mi_ferreteria.Models
{
    public class Cliente
    {
        public long Id { get; set; }
        public string Nombre { get; set; } // Nombre o razón social
        public string? Apellido { get; set; }
        public string? TipoDocumento { get; set; }
        public string? NumeroDocumento { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string TipoCliente { get; set; } = "CONSUMIDOR_FINAL"; // CONSUMIDOR_FINAL | CUENTA_CORRIENTE
        public bool CuentaCorrienteHabilitada { get; set; }
        public decimal LimiteCredito { get; set; }
        public bool Activo { get; set; } = true;
        public DateTimeOffset FechaAlta { get; set; }
    }
}
