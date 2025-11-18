using System;

namespace mi_ferreteria.Models
{
    public class ClienteCuentaCorrienteMovimiento
    {
        public long Id { get; set; }
        public long ClienteId { get; set; }
        public long? VentaId { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public string Tipo { get; set; } // DEUDA | PAGO | AJUSTE
        public decimal Monto { get; set; }
        public string? Descripcion { get; set; }
        public int? UsuarioId { get; set; }
    }
}

