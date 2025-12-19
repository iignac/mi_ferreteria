using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class StockCriticosViewModel
    {
        public IReadOnlyCollection<ProductoStockCritico> Productos { get; set; } = new List<ProductoStockCritico>();
        public string Query { get; set; } = string.Empty;
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }
}
