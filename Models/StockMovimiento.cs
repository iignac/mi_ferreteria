using System;

namespace mi_ferreteria.Models
{
    public class StockMovimiento
    {
        public long Id { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public long ProductoId { get; set; }
        public string Tipo { get; set; } = ""; // INGRESO | EGRESO
        public long Cantidad { get; set; }
        public string? Motivo { get; set; }
        public decimal? PrecioCompra { get; set; }
    }
}
