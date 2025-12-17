namespace mi_ferreteria.Models
{
    public class ReporteFinancieroTopProducto
    {
        public long ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal CantidadVendida { get; set; }
        public decimal ImporteTotal { get; set; }
    }
}
