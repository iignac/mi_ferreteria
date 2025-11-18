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
        
        public IActionResult Index(string? q = null, string? sort = null, int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                // validar sort
                var validSorts = new System.Collections.Generic.HashSet<string>(new[] {
                    "id_desc","id_asc","nombre_asc","nombre_desc","precio_asc","precio_desc","stock_asc","stock_desc"
                }, System.StringComparer.OrdinalIgnoreCase);
                sort = string.IsNullOrWhiteSpace(sort) ? "id_asc" : sort.Trim().ToLowerInvariant();
                if (!validSorts.Contains(sort)) sort = "id_desc";
                int total;
                int totalPages;
                IEnumerable<Producto> productos;

                if (!string.IsNullOrWhiteSpace(q))
                {
                    total = _repo.CountSearch(q);
                    totalPages = (int)Math.Ceiling(total / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page > totalPages) page = totalPages;
                    productos = _repo.SearchPageSorted(q, page, pageSize, sort).ToList();
                }
                else
                {
                    total = _repo.CountAll();
                    totalPages = (int)Math.Ceiling(total / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page > totalPages) page = totalPages;
                    productos = _repo.GetPageSorted(page, pageSize, sort).ToList();
                }
                var stocks = _stockRepo.GetStocks(productos.Select(p => p.Id));
                ViewBag.Stocks = stocks;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;
                ViewBag.Query = q;
                ViewBag.Sort = sort;
                return View(productos);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar productos");
                ViewBag.LoadError = true;
                ViewBag.Page = 1; ViewBag.PageSize = 10; ViewBag.TotalCount = 0; ViewBag.TotalPages = 1; ViewBag.Query = q;
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

        public IActionResult Create(int? page = null)
        {
            var model = new ProductoFormViewModel { Activo = true };
            model.Categorias = _catRepo.GetAll().Where(c => c.Activo).Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                        ViewBag.ReturnPage = page ?? 1;
            ViewBag.ReturnPage = page ?? 1;
                    return View(model);
        }

        [HttpPost]
        public IActionResult Create(ProductoFormViewModel model, int? page = null)
        {
            try
            {
                // Normalización básica
                model.Sku = model.Sku?.Trim();
                model.Nombre = model.Nombre?.Trim();
                model.UbicacionCodigo = model.UbicacionCodigo?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(model.Sku)) ModelState.AddModelError("Sku", "El SKU es obligatorio.");
                if (string.IsNullOrWhiteSpace(model.Nombre)) ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    Response.StatusCode = 409;
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                // Validar categorías existentes
                var categoriasValidas = _catRepo.GetAll().Where(c => c.Activo).Select(c => c.Id).ToHashSet();
                if (model.CategoriaIds != null && model.CategoriaIds.Any(id2 => !categoriasValidas.Contains(id2)))
                {
                    ModelState.AddModelError("CategoriaIds", "Alguna categoría seleccionada no existe.");
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                var p = new Producto
                {
                    Sku = model.Sku,
                    Nombre = model.Nombre,
                    Descripcion = model.Descripcion,
                    CategoriaId = (model.CategoriaIds != null && model.CategoriaIds.Count > 0) ? model.CategoriaIds.First() : (long?)null,
                    PrecioVentaActual = model.PrecioVentaActual,
                    StockMinimo = model.StockMinimo,
                    Activo = model.Activo,
                    UbicacionPreferidaId = model.UbicacionPreferidaId,
                    UbicacionCodigo = model.UbicacionCodigo
                };
                _repo.Add(p);
                // Guardar hasta 3 categorías seleccionadas
                var catIdsCreate = (model.CategoriaIds ?? new System.Collections.Generic.List<long>()).Distinct().Take(3);
                _repo.ReplaceCategorias(p.Id, catIdsCreate);
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
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                _repo.ReplaceBarcodes(p.Id, barcodes);
                TempData["Success"] = $"Producto '{p.Nombre}' creado correctamente.";
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto");
                return Problem("Ocurrió un error al crear el producto.");
            }
        }

        public IActionResult Edit(long id, int? page = null)
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
                    CategoriaIds = _repo.GetCategorias(p.Id).ToList(),
                    PrecioVentaActual = p.PrecioVentaActual,
                    StockMinimo = p.StockMinimo,
                    Activo = p.Activo,
                    UbicacionPreferidaId = p.UbicacionPreferidaId,
                    UbicacionCodigo = p.UbicacionCodigo,
                    OriginalHash = mi_ferreteria.Security.ConcurrencyToken.ComputeProductoHash(p, _repo.GetCategorias(p.Id))
                };
                model.Categorias = _catRepo.GetAll().Where(c => c.Activo).Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaIds.Contains(c.Id)) }).ToList();
                // Cargar barcodes existentes
                var bcs = _repo.GetBarcodes(p.Id);
                model.Barcodes = bcs.Select(x => x.CodigoBarra).ToList();
                ViewBag.ReturnPage = page ?? 1;
                    return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error cargando edición de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar la edición del producto.");
            }
        }

        [HttpPost]
        public IActionResult Edit(ProductoFormViewModel model, int? page = null)
        {
            try
            {
                model.Sku = model.Sku?.Trim();
                model.Nombre = model.Nombre?.Trim();
                model.UbicacionCodigo = model.UbicacionCodigo?.Trim().ToUpperInvariant();
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaIds.Contains(c.Id)) }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                if (string.IsNullOrWhiteSpace(model.Sku)) { ModelState.AddModelError("Sku", "El SKU es obligatorio."); }
                if (string.IsNullOrWhiteSpace(model.Nombre)) { ModelState.AddModelError("Nombre", "El nombre es obligatorio."); }
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaIds.Contains(c.Id)) }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku, model.Id))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    Response.StatusCode = 409;
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                // Concurrencia optimista
                var actual = _repo.GetById(model.Id);
                if (actual == null) return NotFound();
                var hashActual = mi_ferreteria.Security.ConcurrencyToken.ComputeProductoHash(actual, _repo.GetCategorias(actual.Id));
                if (!string.IsNullOrEmpty(model.OriginalHash) && !string.Equals(model.OriginalHash, hashActual, System.StringComparison.Ordinal))
                {
                    Response.StatusCode = 409;
                    ModelState.AddModelError(string.Empty, "El producto fue modificado por otro proceso. Recarga la pA!gina.");
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaIds.Contains(c.Id)) }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                // Validar categorías existentes
                var categoriasValidas = _catRepo.GetAll().Where(c => c.Activo).Select(c => c.Id).ToHashSet();
                if (model.CategoriaIds != null && model.CategoriaIds.Any(id2 => !categoriasValidas.Contains(id2)))
                {
                    ModelState.AddModelError("CategoriaIds", "Alguna categoría seleccionada no existe.");
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaIds.Contains(c.Id)) }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                var p = _repo.GetById(model.Id);
                if (p == null) return NotFound();
                p.Sku = model.Sku;
                p.Nombre = model.Nombre;
                p.Descripcion = model.Descripcion;
                p.CategoriaId = (model.CategoriaIds != null && model.CategoriaIds.Count > 0) ? model.CategoriaIds.First() : (long?)null;
                p.PrecioVentaActual = model.PrecioVentaActual;
                p.StockMinimo = model.StockMinimo;
                p.Activo = model.Activo;
                p.UbicacionPreferidaId = model.UbicacionPreferidaId;
                p.UbicacionCodigo = model.UbicacionCodigo;
                _repo.Update(p);
                // Guardar hasta 3 categorías seleccionadas
                var catIdsEdit = (model.CategoriaIds ?? new System.Collections.Generic.List<long>()).Distinct().Take(3);
                _repo.ReplaceCategorias(p.Id, catIdsEdit);
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
                    model.Categorias = _catRepo.GetAll().Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = (model.CategoriaIds.Contains(c.Id)) }).ToList();
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                _repo.ReplaceBarcodes(p.Id, barcodes);
                TempData["Success"] = $"Producto '{p.Nombre}' actualizado correctamente.";
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar producto {ProductoId}", model.Id);
                return Problem("Ocurrió un error al actualizar el producto.");
            }
        }

        public IActionResult Delete(long id, int? page = null)
        {
            try
            {
                var p = _repo.GetById(id);
                if (p == null) return NotFound();
                ViewBag.ReturnPage = page ?? 1;
                return View(p);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error cargando eliminación de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar la eliminación del producto.");
            }
        }

        public IActionResult Details(long id, int? page = null)
        {
            try
            {
                var p = _repo.GetById(id);
                if (p == null) return NotFound();
                var barcodes = _repo.GetBarcodes(id);
                var stock = _stockRepo.GetStock(id);
                var catIds = _repo.GetCategorias(id).ToList();
                if (!catIds.Any() && p.CategoriaId.HasValue) catIds.Add(p.CategoriaId.Value);
                var catNames = new System.Collections.Generic.List<string>();
                foreach (var cid in catIds)
                {
                    var c = _catRepo.GetById(cid);
                    if (c != null && !string.IsNullOrWhiteSpace(c.Nombre)) catNames.Add(c.Nombre);
                }
                ViewBag.Barcodes = barcodes;
                ViewBag.Stock = stock;
                ViewBag.CategoriasNombres = catNames;
                ViewBag.ReturnPage = page ?? 1;
                return View(p);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalles de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar los detalles del producto.");
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(long id, int? page = null)
        {
            try
            {
                var prod = _repo.GetById(id);
                var nombre = prod?.Nombre ?? ("#" + id);
                _repo.Delete(id);
                TempData["Success"] = $"Producto '{nombre}' eliminado correctamente.";
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar producto {ProductoId}", id);
                return Problem("Ocurrió un error al eliminar el producto.");
            }
        }

    }
}

