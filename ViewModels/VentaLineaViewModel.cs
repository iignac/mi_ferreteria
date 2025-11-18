using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.ViewModels
{
    public class VentaLineaViewModel
    {
        [Required]
        public long ProductoId { get; set; }

        public string? ProductoNombre { get; set; }

        [Range(0.01, 9999999999.99)]
        public decimal Cantidad { get; set; }

        [Range(0, 9999999999.99)]
        public decimal PrecioUnitario { get; set; }

        public long StockDisponible { get; set; }

        public bool PermitirVentaSinStock { get; set; }
    }
}

