using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;

namespace mi_ferreteria.Controllers
{
    public class StockProductoController : Controller
    {
        private readonly IProductoRepository _prodRepo;
        private readonly IStockRepository _stockRepo;
        private readonly ILogger<StockProductoController> _logger;

        public StockProductoController(IProductoRepository prodRepo, IStockRepository stockRepo, ILogger<StockProductoController> logger)
        {
            _prodRepo = prodRepo;
            _stockRepo = stockRepo;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Manage(long id, int ingresosPage = 1, int egresosPage = 1)
        {
            var p = _prodRepo.GetById(id);
            if (p == null) return NotFound();
            ViewBag.Producto = p;
            ViewBag.Stock = _stockRepo.GetStock(id);
            const int pageSize = 5;
            if (ingresosPage < 1) ingresosPage = 1;
            if (egresosPage < 1) egresosPage = 1;
            var ingresosTotal = _stockRepo.CountMovimientos(id, "INGRESO");
            var ingresosPages = (int)System.Math.Ceiling(ingresosTotal / (double)pageSize);
            if (ingresosPages == 0) ingresosPages = 1;
            if (ingresosPage > ingresosPages) ingresosPage = ingresosPages;

            var egresosTotal = _stockRepo.CountMovimientos(id, "EGRESO");
            var egresosPages = (int)System.Math.Ceiling(egresosTotal / (double)pageSize);
            if (egresosPages == 0) egresosPages = 1;
            if (egresosPage > egresosPages) egresosPage = egresosPages;

            ViewBag.Ingresos = _stockRepo.GetMovimientosPage(id, "INGRESO", ingresosPage, pageSize);
            ViewBag.Egresos = _stockRepo.GetMovimientosPage(id, "EGRESO", egresosPage, pageSize);
            ViewBag.IngresosPage = ingresosPage;
            ViewBag.IngresosTotalPages = ingresosPages;
            ViewBag.EgresosPage = egresosPage;
            ViewBag.EgresosTotalPages = egresosPages;
            return View();
        }

        [HttpPost]
        public IActionResult Manage(long id, string tipo, long cantidad, string? motivo)
        {
            try
            {
                if (cantidad <= 0)
                {
                    TempData["StockError"] = "La cantidad debe ser mayor a 0.";
                    return RedirectToAction("Manage", new { id });
                }
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    TempData["StockError"] = "El motivo es obligatorio.";
                    return RedirectToAction("Manage", new { id });
                }
                if (tipo == "INGRESO") _stockRepo.Ingresar(id, cantidad, motivo!);
                else if (tipo == "EGRESO") _stockRepo.Egresar(id, cantidad, motivo!);
                else TempData["StockError"] = "Tipo inválido";
                return RedirectToAction("Manage", new { id });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error ajustando stock {ProductoId}", id);
                TempData["StockError"] = "Ocurrió un error al ajustar el stock.";
                return RedirectToAction("Manage", new { id });
            }
        }
    }
}
