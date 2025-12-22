using System.Collections.Generic;
using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IAuditoriaRepository
    {
        void Registrar(int usuarioId, string usuarioNombre, string accion, string? detalle = null);
        (IEnumerable<AuditoriaRegistro> Registros, int Total) GetPage(int page, int pageSize, string? accionFiltro = null);
    }
}
