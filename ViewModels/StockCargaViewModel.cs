using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.ViewModels
{
    public class StockCargaViewModel
    {
        public List<StockCargaLineaViewModel> Lineas { get; set; } = new();

        [StringLength(200)]
        public string? Motivo { get; set; }

        [Required]
        [RegularExpression("INGRESO|EGRESO", ErrorMessage = "Tipo de movimiento invalido.")]
        public string TipoMovimiento { get; set; } = "INGRESO";
    }

    public class StockCargaLineaViewModel
    {
        [Required]
        public long ProductoId { get; set; }

        public string ProductoNombre { get; set; } = string.Empty;

        public string UnidadMedida { get; set; } = "unidad";

        public long StockActual { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public long Cantidad { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El precio de compra no puede ser negativo")]
        public decimal? PrecioCompra { get; set; }
    }
}
