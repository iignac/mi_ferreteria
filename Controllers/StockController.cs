using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace mi_ferreteria.Controllers
{
    public class StockController : Controller
    {
        private readonly ILogger<StockController> _logger;

        public StockController(ILogger<StockController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Cargando vista de Stock");
                return View();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en vista Stock");
                return Problem("Ocurrió un error al cargar la vista de stock.");
            }
        }
    }
}
