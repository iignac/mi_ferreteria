using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class VentaPendienteItemViewModel
    {
        public Venta Venta { get; set; }
        public Cliente? Cliente { get; set; }
        public decimal SaldoActual { get; set; }
        public decimal SaldoPostVenta { get; set; }
        public decimal LimiteCredito => Cliente?.LimiteCredito ?? 0;
    }
}
