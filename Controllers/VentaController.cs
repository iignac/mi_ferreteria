using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace mi_ferreteria.Controllers
{
    public class VentaController : Controller
    {
        private readonly ILogger<VentaController> _logger;

        public VentaController(ILogger<VentaController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Cargando vista de Venta");
                return View();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en vista Venta");
                return Problem("Ocurrió un error al cargar la vista de venta.");
            }
        }
    }
}
