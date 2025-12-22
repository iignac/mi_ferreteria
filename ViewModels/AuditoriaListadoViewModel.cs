using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.ViewModels
{
    public class AuditoriaListadoViewModel
    {
        public List<AuditoriaRegistro> Registros { get; set; } = new List<AuditoriaRegistro>();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public string? SearchTerm { get; set; }
    }
}
