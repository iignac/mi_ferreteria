using System;

namespace mi_ferreteria.Models
{
    public class Cliente
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string? TipoDocumento { get; set; }
        public string? NumeroDocumento { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public decimal LimiteCredito { get; set; }
        public bool Activo { get; set; } = true;
        public DateTimeOffset FechaAlta { get; set; }
    }
}

