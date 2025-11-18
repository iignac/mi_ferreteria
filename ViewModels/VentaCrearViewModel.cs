using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace mi_ferreteria.ViewModels
{
    public class VentaCrearViewModel
    {
        [Display(Name = "Cliente")]
        public long? ClienteId { get; set; }

        [Required]
        [Display(Name = "Tipo de cliente")]
        public string TipoCliente { get; set; } = "CONSUMIDOR_FINAL"; // CONSUMIDOR_FINAL | REGISTRADO

        [Required]
        [Display(Name = "Forma de pago")]
        public string TipoPago { get; set; } = "CONTADO"; // CONTADO | CUENTA_CORRIENTE

        public bool IgnorarLimiteCredito { get; set; }

        public decimal Total { get; set; }

        public List<VentaLineaViewModel> Lineas { get; set; } = new();

        public List<SelectListItem> Clientes { get; set; } = new();
    }
}

