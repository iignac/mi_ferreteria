using System;
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
        public string? SelectedModulo { get; set; }
        public string? SelectedModuloLabel { get; set; }
        public string? SelectedOperacion { get; set; }
        public string? SelectedOperacionLabel { get; set; }
        public DateTimeOffset? FechaDesde { get; set; }
        public DateTimeOffset? FechaHasta { get; set; }
        public List<FiltroOpcion> ModulosDisponibles { get; set; } = new List<FiltroOpcion>();
        public List<FiltroOpcion> OperacionesDisponibles { get; set; } = new List<FiltroOpcion>();

        public bool TieneFiltrosActivos =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            !string.IsNullOrWhiteSpace(SelectedModulo) ||
            !string.IsNullOrWhiteSpace(SelectedOperacion) ||
            FechaDesde.HasValue ||
            FechaHasta.HasValue;
    }

    public class FiltroOpcion
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
