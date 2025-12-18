using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class FinanzasDashboardViewModel
    {
        public ReporteFinancieroResumen Resumen { get; set; } = new ReporteFinancieroResumen();
        public int DiasTopProductos { get; set; } = 30;
        public int DiasTopClientes { get; set; } = 30;
    }
}
