namespace mi_ferreteria.Models
{
    public class VentaDetalle
    {
        public long Id { get; set; }
        public long VentaId { get; set; }
        public long ProductoId { get; set; }
        public string Descripcion { get; set; }
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public bool PermiteVentaSinStock { get; set; }
    }
}

