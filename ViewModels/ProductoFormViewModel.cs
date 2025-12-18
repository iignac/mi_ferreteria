using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

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

        [StringLength(1000, ErrorMessage = "La descripcion es demasiado larga")]
        public string? Descripcion { get; set; }

        // Permite hasta 3 categorias seleccionadas
        [MinLength(1, ErrorMessage = "Debes seleccionar al menos una categoria.")]
        public List<long> CategoriaIds { get; set; } = new();
        public List<SelectListItem> Categorias { get; set; } = new();
        public List<SelectListItem> UnidadesMedida { get; set; } = new();

        [Required(ErrorMessage = "El precio es obligatorio")]
        [Range(0, 9999999999.99, ErrorMessage = "El precio debe ser >= 0")]
        public decimal PrecioVentaActual { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock minimo debe ser >= 0")]
        public int StockMinimo { get; set; }

        [Required(ErrorMessage = "La unidad de medida es obligatoria")]
        public string UnidadMedida { get; set; } = "unidad";

        public bool Activo { get; set; } = true;

        public long? UbicacionPreferidaId { get; set; }
        [RegularExpression("^[A-Za-z][0-9]{1,2}$", ErrorMessage = "La ubicacion debe tener formato letra+numero, ej: A1")]
        public string? UbicacionCodigo { get; set; }

        // Codigos de barra: coleccion de inputs dinamicos en la vista
        public List<string> Barcodes { get; set; } = new();
        public string? OriginalHash { get; set; }
    }
}
