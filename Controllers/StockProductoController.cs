using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
        public IActionResult Manage(long id, int page = 1)
        {
            var p = _prodRepo.GetById(id);
            if (p == null) return NotFound();
            ViewBag.Producto = p;
            ViewBag.Stock = _stockRepo.GetStock(id);
            const int pageSize = 5;
            if (page < 1) page = 1;
            var total = _stockRepo.CountMovimientos(id, null);
            var totalPages = (int)System.Math.Ceiling(total / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            ViewBag.Movimientos = _stockRepo.GetMovimientosPage(id, null, page, pageSize);
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Manage(long id, string tipo, long cantidad, string? motivo)
        {
            try
            {
                if (!PuedeMoverStock()) return Forbid();

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

        private bool PuedeMoverStock()
        {
            return User.IsInRole("Administrador") || User.IsInRole("Stock");
        }
    }
}
