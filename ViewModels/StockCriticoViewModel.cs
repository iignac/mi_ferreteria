namespace mi_ferreteria.ViewModels
{
    public class StockCriticoViewModel
    {
        public long ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public long StockActual { get; set; }
        public int StockMinimo { get; set; }
    }
}
