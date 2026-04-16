using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class ClienteFacturasViewModel
    {
        public Cliente Cliente { get; set; }
        public List<ClienteFacturaItemViewModel> Facturas { get; set; } = new();
        public string Query { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; } = 1;
        public int TotalFacturas { get; set; }
    }
}
