using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace mi_ferreteria.ViewModels
{
    public class ImportarProductosViewModel
    {
        public IFormFile Archivo { get; set; }
    }

    public class ImportarProductoResultado
    {
        public int Fila { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarra { get; set; }
        public string Estado { get; set; } // "creado", "actualizado", "no_encontrado", "error"
        public string Mensaje { get; set; }
    }

    public class ImportarProductosResultadoViewModel
    {
        public int TotalCreados { get; set; }
        public int TotalActualizados { get; set; }
        public int TotalNoEncontrados { get; set; }
        public int TotalErrores { get; set; }
        public List<ImportarProductoResultado> Resultados { get; set; } = new();
    }
}
