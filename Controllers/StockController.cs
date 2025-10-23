using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
// holahola
namespace mi_ferreteria.Controllers
{
    public class StockController : Controller
    {
        private readonly ILogger<StockController> _logger;
        private readonly IStockRepository _stockRepo;
        private readonly IProductoRepository _prodRepo;

        public StockController(ILogger<StockController> logger, IStockRepository stockRepo, IProductoRepository prodRepo)
        {
            _logger = logger;
            _stockRepo = stockRepo;
            _prodRepo = prodRepo;
        }

        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Cargando vista de Stock");
                var ultIngresos = _stockRepo.GetUltimosMovimientos("INGRESO", 10);
                var ultEgresos = _stockRepo.GetUltimosMovimientos("EGRESO", 10);
                // Construir diccionario de nombres de productos
                var ids = ultIngresos.Select(x => x.ProductoId).Concat(ultEgresos.Select(x => x.ProductoId)).Distinct().ToList();
                var names = new System.Collections.Generic.Dictionary<long, string>();
                foreach (var pid in ids)
                {
                    var p = _prodRepo.GetById(pid);
                    names[pid] = p?.Nombre ?? ("#" + pid);
                }
                ViewBag.UltimosIngresos = ultIngresos;
                ViewBag.UltimosEgresos = ultEgresos;
                ViewBag.ProductNames = names;
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



