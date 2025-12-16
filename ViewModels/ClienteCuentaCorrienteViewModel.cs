using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class ClienteCuentaCorrienteViewModel
    {
        public Cliente Cliente { get; set; }
        public List<ClienteCuentaCorrienteMovimiento> Movimientos { get; set; } = new();
        public List<ClienteCuentaCorrienteFacturaPendiente> FacturasVencidas { get; set; } = new();
        public List<ClienteCuentaCorrienteFacturaPendiente> FacturasPendientes { get; set; } = new();
        public decimal SaldoActual { get; set; }
    }
}
