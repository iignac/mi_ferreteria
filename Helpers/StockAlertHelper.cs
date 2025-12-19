using System.Collections.Generic;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;

namespace mi_ferreteria.Helpers
{
    public static class StockAlertHelper
    {
        public static StockAlertSnapshot Build(IEnumerable<Producto>? productos, IDictionary<long, long>? stockActual)
        {
            var snapshot = new StockAlertSnapshot();
            if (productos == null) return snapshot;

            foreach (var producto in productos)
            {
                if (producto == null) continue;
                var actual = 0L;
                if (stockActual != null && stockActual.TryGetValue(producto.Id, out var valor))
                {
                    actual = valor;
                }
                else if (stockActual == null)
                {
                    // nada: se asume 0
                }
                var esCritico = producto.StockMinimo > 0 && actual <= producto.StockMinimo;
                if (!esCritico) continue;

                snapshot.Criticos.Add(producto.Id);
                snapshot.Detalles.Add(new StockCriticoViewModel
                {
                    ProductoId = producto.Id,
                    Nombre = producto.Nombre ?? $"Producto #{producto.Id}",
                    StockActual = actual,
                    StockMinimo = producto.StockMinimo
                });
            }

            snapshot.TotalCriticos = snapshot.Detalles.Count;
            return snapshot;
        }
    }

    public class StockAlertSnapshot
    {
        public HashSet<long> Criticos { get; } = new HashSet<long>();
        public List<StockCriticoViewModel> Detalles { get; } = new List<StockCriticoViewModel>();
        public int TotalCriticos { get; set; }
    }
}
