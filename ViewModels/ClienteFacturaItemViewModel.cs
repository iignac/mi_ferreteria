using System;

namespace mi_ferreteria.ViewModels
{
    public class ClienteFacturaItemViewModel
    {
        public long VentaId { get; set; }
        public long FacturaId { get; set; }
        public DateTimeOffset FechaEmision { get; set; }
        public string TipoComprobante { get; set; } = string.Empty;
        public int PuntoVenta { get; set; }
        public long Numero { get; set; }
        public decimal Total { get; set; }
        public string TipoPago { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string ComprobanteFormateado { get; set; } = string.Empty;
    }
}
