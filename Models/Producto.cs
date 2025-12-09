using System;

namespace mi_ferreteria.Models
{
    public class Producto
    {
        public long Id { get; set; }
        public string Sku { get; set; }
        public string Nombre { get; set; }
        public string? Descripcion { get; set; }
        public long? CategoriaId { get; set; }
        public decimal PrecioVentaActual { get; set; }
        public int StockMinimo { get; set; }
        public string UnidadMedida { get; set; } = "unidad";
        public bool Activo { get; set; } = true;
        public long? UbicacionPreferidaId { get; set; }
        public string? UbicacionCodigo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
