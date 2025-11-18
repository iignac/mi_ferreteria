using System;

namespace mi_ferreteria.Models
{
    public class Factura
    {
        public long Id { get; set; }
        public long VentaId { get; set; }
        public string TipoComprobante { get; set; } = "FACTURA_B";
        public int PuntoVenta { get; set; } = 1;
        public long Numero { get; set; }
        public DateTimeOffset FechaEmision { get; set; }
        public decimal Total { get; set; }
        public string TotalEnLetras { get; set; }
        public string ClienteNombre { get; set; }
        public string? ClienteDocumento { get; set; }
        public string? ClienteDireccion { get; set; }
    }
}

