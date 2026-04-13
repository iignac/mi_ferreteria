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
        public decimal SaldoDisponible { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalMovimientos { get; set; }
        public int TotalPages { get; set; } = 1;
    }
}
