namespace mi_ferreteria.Models
{
    public class ProductoCodigoBarra
    {
        public long Id { get; set; }
        public long ProductoId { get; set; }
        public string CodigoBarra { get; set; }
        public string? Tipo { get; set; }
    }
}

