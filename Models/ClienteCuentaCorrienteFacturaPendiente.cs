using System;

namespace mi_ferreteria.Models
{
    public class ClienteCuentaCorrienteFacturaPendiente
    {
        public long MovimientoDeudaId { get; set; }
        public long VentaId { get; set; }
        public string? Comprobante { get; set; }
        public DateTimeOffset FechaEmision { get; set; }
        public DateTimeOffset FechaVencimiento { get; set; }
        public decimal ImporteOriginal { get; set; }
        public decimal SaldoPendiente { get; set; }
    }
}
