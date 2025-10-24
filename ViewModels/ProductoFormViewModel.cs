using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace mi_ferreteria.ViewModels
{
    public class ProductoFormViewModel
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "El SKU es obligatorio")]
        [StringLength(32, ErrorMessage = "El SKU debe tener hasta 32 caracteres")]
        public string Sku { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150, ErrorMessage = "El nombre debe tener hasta 150 caracteres")]
        public string Nombre { get; set; }

        [StringLength(1000, ErrorMessage = "La descripción es demasiado larga")]
        public string? Descripcion { get; set; }

        // Permite hasta 3 categorías seleccionadas
        public List<long> CategoriaIds { get; set; } = new();
        public List<SelectListItem> Categorias { get; set; } = new();

        [Required(ErrorMessage = "El precio es obligatorio")]
        [Range(0, 9999999999.99, ErrorMessage = "El precio debe ser >= 0")]
        public decimal PrecioVentaActual { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo debe ser >= 0")]
        public int StockMinimo { get; set; }

        public bool Activo { get; set; } = true;

        public long? UbicacionPreferidaId { get; set; }
        [RegularExpression("^[A-Za-z][0-9]{1,2}$", ErrorMessage = "La ubicación debe tener formato letra+número, ej: A1")]
        public string? UbicacionCodigo { get; set; }

        // Códigos de barra: colección de inputs dinámicos en la vista
        public List<string> Barcodes { get; set; } = new();
        public string? OriginalHash { get; set; }
    }
}




