namespace mi_ferreteria.Models
{
    public class ProductoStockCritico
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public long StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string UnidadMedida { get; set; } = "unidad";
        public string? UbicacionCodigo { get; set; }
        public decimal PrecioVentaActual { get; set; }
    }
}
