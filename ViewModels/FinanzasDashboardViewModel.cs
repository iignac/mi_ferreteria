using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class FinanzasDashboardViewModel
    {
        public ReporteFinancieroResumen Resumen { get; set; } = new ReporteFinancieroResumen();
        public int DiasTopProductos { get; set; } = 30;
        public int DiasTopClientes { get; set; } = 30;
        public long? CategoriaTopProductosId { get; set; }
        public List<Categoria> Categorias { get; set; } = new List<Categoria>();
    }
}
