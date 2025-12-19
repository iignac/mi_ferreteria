using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalVentas { get; set; }
        public int TotalMovimientosStock { get; set; }
        public int TotalMovimientosIngreso { get; set; }
        public int TotalMovimientosEgreso { get; set; }
        public int TotalProductos { get; set; }
        public int ProductosInactivos { get; set; }

        public IReadOnlyCollection<Venta> UltimasVentas { get; set; } = new List<Venta>();
        public IReadOnlyCollection<StockMovimiento> UltimosMovimientosStock { get; set; } = new List<StockMovimiento>();
        public IReadOnlyCollection<Producto> UltimasAltas { get; set; } = new List<Producto>();
        public IReadOnlyCollection<Producto> UltimasBajas { get; set; } = new List<Producto>();
        public IReadOnlyCollection<Producto> UltimasEdiciones { get; set; } = new List<Producto>();
        public IReadOnlyCollection<Usuario> AdministradoresActivos { get; set; } = new List<Usuario>();
        public IReadOnlyCollection<string> AlertasSeguridad { get; set; } = new List<string>();
        public IDictionary<long, string> ProductosPorId { get; set; } = new Dictionary<long, string>();
    }
}
