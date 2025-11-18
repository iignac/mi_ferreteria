using System;
using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class VentaComprobanteViewModel
    {
        public Venta Venta { get; set; }
        public List<VentaDetalle> Detalles { get; set; }
        public Factura? Factura { get; set; }
    }
}

