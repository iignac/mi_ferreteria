using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using System.Linq;

namespace mi_ferreteria.Controllers
{
    public class ProductoController : Controller
    {
        private readonly IProductoRepository _repo;
        private readonly ICategoriaRepository _catRepo;
        private readonly IStockRepository _stockRepo;
        private readonly ILogger<ProductoController> _logger;

        public ProductoController(IProductoRepository repo, ICategoriaRepository catRepo, IStockRepository stockRepo, ILogger<ProductoController> logger)
        {
            _repo = repo;
            _catRepo = catRepo;
            _stockRepo = stockRepo;
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                var productos = _repo.GetAll().ToList();
                var stocks = productos.ToDictionary(p => p.Id, p => _stockRepo.GetStock(p.Id));
                ViewBag.Stocks = stocks;
                return View(productos);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar productos");
                ViewBag.LoadError = true;
                return View(Enumerable.Empty<Producto>());
            }
        }

        // Utilidad: parsear lista de input de códigos de barra (solo código; tipo opcional si se desea ampliar)
        private static System.Collections.Generic.List<mi_ferreteria.Models.ProductoCodigoBarra> ParseBarcodes(System.Collections.Generic.IEnumerable<string>? codes)
        {
            var list = new System.Collections.Generic.List<mi_ferreteria.Models.ProductoCodigoBarra>();
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (codes == null) return list;
            foreach (var raw in codes)
            {
                var t = raw?.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                // Si alguien ingresa "codigo,tipo" lo separamos; si no, tomamos solo codigo
                var parts = t.Split(new[] { ',', ';' }, 2, System.StringSplitOptions.RemoveEmptyEntries);
                var code = parts[0].Trim();
                if (seen.Contains(code)) continue;
                seen.Add(code);
                string? tipo = parts.Length > 1 ? parts[1].Trim() : null;
                list.Add(new mi_ferreteria.Models.ProductoCodigoBarra { CodigoBarra = code, Tipo = tipo });
            }
            return list;
        }

        public IActionResult Create()
        {
            var model = new ProductoFormViewModel { Activo = true };
            model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
            return View(model);
        }

        [HttpPost]
        public IActionResult Create(ProductoFormViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe.");
                    return View(model);
                }
                var p = new Producto
                {
                    Sku = model.Sku,
                    Nombre = model.Nombre,
                    Descripcion = model.Descripcion,
                    CategoriaId = model.CategoriaId,
                    PrecioVentaActual = model.PrecioVentaActual,
                    StockMinimo = model.StockMinimo,
                    Activo = model.Activo,
                    UbicacionPreferidaId = model.UbicacionPreferidaId
                };
                _repo.Add(p);
                // Parsear códigos de barra desde la lista
                var barcodes = ParseBarcodes(model.Barcodes);
                // Validar unicidad global
                foreach (var bc in barcodes)
                {
                    if (_repo.BarcodeExists(bc.CodigoBarra))
                    {
                        ModelState.AddModelError("CodigosBarra", $"El código de barra {bc.CodigoBarra} ya existe");
                    }
                }
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    return View(model);
                }
                _repo.ReplaceBarcodes(p.Id, barcodes);
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto");
                return Problem("Ocurrió un error al crear el producto.");
            }
        }

        public IActionResult Edit(long id)
        {
            try
            {
                var p = _repo.GetById(id);
                if (p == null) return NotFound();
                var model = new ProductoFormViewModel
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    CategoriaId = p.CategoriaId,
                    PrecioVentaActual = p.PrecioVentaActual,
                    StockMinimo = p.StockMinimo,
                    Activo = p.Activo,
                    UbicacionPreferidaId = p.UbicacionPreferidaId
                };
                model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaId == c.Id) }).ToList();
                // Cargar barcodes existentes
                var bcs = _repo.GetBarcodes(p.Id);
                model.Barcodes = bcs.Select(x => x.CodigoBarra).ToList();
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error cargando edición de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar la edición del producto.");
            }
        }

        [HttpPost]
        public IActionResult Edit(ProductoFormViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaId == c.Id) }).ToList();
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku, model.Id))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    return View(model);
                }
                var p = _repo.GetById(model.Id);
                if (p == null) return NotFound();
                p.Sku = model.Sku;
                p.Nombre = model.Nombre;
                p.Descripcion = model.Descripcion;
                p.CategoriaId = model.CategoriaId;
                p.PrecioVentaActual = model.PrecioVentaActual;
                p.StockMinimo = model.StockMinimo;
                p.Activo = model.Activo;
                p.UbicacionPreferidaId = model.UbicacionPreferidaId;
                _repo.Update(p);
                // Validar y reemplazar códigos de barra
                var barcodes = ParseBarcodes(model.Barcodes);
                foreach (var bc in barcodes)
                {
                    if (_repo.BarcodeExists(bc.CodigoBarra, model.Id))
                    {
                        ModelState.AddModelError("CodigosBarra", $"El código de barra {bc.CodigoBarra} ya existe en otro producto");
                    }
                }
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaId == c.Id) }).ToList();
                    return View(model);
                }
                _repo.ReplaceBarcodes(p.Id, barcodes);
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar producto {ProductoId}", model.Id);
                return Problem("Ocurrió un error al actualizar el producto.");
            }
        }

        public IActionResult Delete(long id)
        {
            try
            {
                var p = _repo.GetById(id);
                if (p == null) return NotFound();
                return View(p);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error cargando eliminación de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar la eliminación del producto.");
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(long id)
        {
            try
            {
                _repo.Delete(id);
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar producto {ProductoId}", id);
                return Problem("Ocurrió un error al eliminar el producto.");
            }
        }
    }
}
