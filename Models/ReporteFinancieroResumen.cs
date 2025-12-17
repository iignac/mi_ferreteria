using System.Collections.Generic;

namespace mi_ferreteria.Models
{
    public class ReporteFinancieroResumen
    {
        public decimal TotalVendido { get; set; }
        public decimal VentasDia { get; set; }
        public decimal VentasSemana { get; set; }
        public decimal VentasMes { get; set; }
        public decimal VentasAnio { get; set; }
        public decimal MargenBrutoEstimado { get; set; }
        public double? PromedioCobroDias { get; set; }
        public List<ReporteFinancieroTopProducto> TopProductos { get; set; } = new List<ReporteFinancieroTopProducto>();
        public List<ReporteFinancieroTopCliente> TopClientes { get; set; } = new List<ReporteFinancieroTopCliente>();
        public List<ReporteFinancieroDeudor> Deudores { get; set; } = new List<ReporteFinancieroDeudor>();
        public decimal DeudaTotal { get; set; }
    }
}
