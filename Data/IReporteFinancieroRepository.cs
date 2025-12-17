using mi_ferreteria.Models;

namespace mi_ferreteria.Data
{
    public interface IReporteFinancieroRepository
    {
        ReporteFinancieroResumen ObtenerResumen();
    }
}
