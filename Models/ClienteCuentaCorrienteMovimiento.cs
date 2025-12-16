using System;

namespace mi_ferreteria.Models
{
    public class ClienteCuentaCorrienteMovimiento
    {
        public long Id { get; set; }
        public long ClienteId { get; set; }
        public long? VentaId { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public DateTimeOffset? FechaVencimiento { get; set; }
        public long? MovimientoRelacionadoId { get; set; }
        public string Tipo { get; set; } // DEUDA | PAGO | AJUSTE | NOTA_DEBITO | NOTA_CREDITO
        public decimal Monto { get; set; }
        public string? Descripcion { get; set; }
        public int? UsuarioId { get; set; }

        // Datos enriquecidos para la vista (no se persisten)
        public string? Comprobante { get; set; }
        public DateTimeOffset? ComprobanteFecha { get; set; }
        public decimal SaldoAcumulado { get; set; }
    }
}
