using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace mi_ferreteria.Controllers
{
    public class ProductoController : Controller
    {
        private readonly IProductoRepository _repo;
        private readonly ICategoriaRepository _catRepo;
        private readonly IStockRepository _stockRepo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly ILogger<ProductoController> _logger;

        private static readonly string[] UnidadesPermitidas = new[]
        {
            "unidad","gramos","kilos","metros cuadrados","juego","bolsa","placa","rollo","litro","mililitro","bidon","kit","par"
        };

        public ProductoController(IProductoRepository repo, ICategoriaRepository catRepo, IStockRepository stockRepo, IAuditoriaRepository auditoriaRepo, ILogger<ProductoController> logger)
        {
            _repo = repo;
            _catRepo = catRepo;
            _stockRepo = stockRepo;
            _auditoriaRepo = auditoriaRepo;
            _logger = logger;
        }

        public IActionResult Index(string? q = null, string? sort = null, int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                var validSorts = new HashSet<string>(new[]
                {
                    "id_desc","id_asc","nombre_asc","nombre_desc","precio_asc","precio_desc","stock_asc","stock_desc"
                }, StringComparer.OrdinalIgnoreCase);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar productos");
                ViewBag.LoadError = true;
                ViewBag.Page = 1; ViewBag.PageSize = 10; ViewBag.TotalCount = 0; ViewBag.TotalPages = 1; ViewBag.Query = q;
                return View(Enumerable.Empty<Producto>());
            }
        }

        private static List<ProductoCodigoBarra> ParseBarcodes(IEnumerable<string>? codes)
        {
            var list = new List<ProductoCodigoBarra>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (codes == null) return list;
            foreach (var raw in codes)
            {
                var t = raw?.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                var parts = t.Split(new[] { ',', ';' }, 2, StringSplitOptions.RemoveEmptyEntries);
                var code = parts[0].Trim();
                if (seen.Contains(code)) continue;
                seen.Add(code);
                string? tipo = parts.Length > 1 ? parts[1].Trim() : null;
                list.Add(new ProductoCodigoBarra { CodigoBarra = code, Tipo = tipo });
            }
            return list;
        }

        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Create(int? page = null)
        {
            var model = new ProductoFormViewModel { Activo = true, UnidadMedida = "unidad" };
            model.Categorias = _catRepo.GetAll().Where(c => c.Activo).Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
            model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
            ViewBag.ReturnPage = page ?? 1;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Create(ProductoFormViewModel model, int? page = null)
        {
            try
            {
                model.Sku = model.Sku?.Trim();
                model.Nombre = model.Nombre?.Trim();
                model.UbicacionCodigo = model.UbicacionCodigo?.Trim().ToUpperInvariant();
                model.UnidadMedida = (model.UnidadMedida ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(model.Sku)) ModelState.AddModelError("Sku", "El SKU es obligatorio.");
                if (string.IsNullOrWhiteSpace(model.Nombre)) ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
                if (string.IsNullOrWhiteSpace(model.UnidadMedida) || !UnidadesPermitidas.Contains(model.UnidadMedida))
                {
                    ModelState.AddModelError("UnidadMedida", "Seleccione una unidad de medida válida.");
                }
                if (model.CategoriaIds == null || model.CategoriaIds.Count == 0)
                {
                    ModelState.AddModelError("CategoriaIds", "Debes seleccionar al menos una categoria.");
                }
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    Response.StatusCode = 409;
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }

                var categoriasValidas = _catRepo.GetAll().Where(c => c.Activo).Select(c => c.Id).ToHashSet();
                if (model.CategoriaIds != null && model.CategoriaIds.Any(id2 => !categoriasValidas.Contains(id2)))
                {
                    ModelState.AddModelError("CategoriaIds", "Alguna categoria seleccionada no existe.");
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
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
                    UnidadMedida = model.UnidadMedida,
                    Activo = model.Activo,
                    UbicacionPreferidaId = model.UbicacionPreferidaId,
                    UbicacionCodigo = model.UbicacionCodigo
                };
                _repo.Add(p);

                var catIdsCreate = (model.CategoriaIds ?? new List<long>()).Distinct().Take(3);
                _repo.ReplaceCategorias(p.Id, catIdsCreate);

                var barcodes = ParseBarcodes(model.Barcodes);
                foreach (var bc in barcodes)
                {
                    if (_repo.BarcodeExists(bc.CodigoBarra))
                    {
                        ModelState.AddModelError("CodigosBarra", $"El código de barra {bc.CodigoBarra} ya existe");
                    }
                }
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }

                _repo.ReplaceBarcodes(p.Id, barcodes);
                RegistrarAuditoria("CREADO", $"Producto #{p.Id}: {p.Nombre} (SKU {p.Sku}, Precio {p.PrecioVentaActual}, Activo={p.Activo})");
                TempData["Success"] = $"Producto '{p.Nombre}' creado correctamente.";
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto");
                return Problem("Ocurrió un error al crear el producto.");
            }
        }

        [Authorize(Roles = "Administrador,Stock")]
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
                    UnidadMedida = string.IsNullOrWhiteSpace(p.UnidadMedida) ? "unidad" : p.UnidadMedida,
                    Activo = p.Activo,
                    UbicacionPreferidaId = p.UbicacionPreferidaId,
                    UbicacionCodigo = p.UbicacionCodigo,
                    OriginalHash = mi_ferreteria.Security.ConcurrencyToken.ComputeProductoHash(p, _repo.GetCategorias(p.Id))
                };
                model.Categorias = _catRepo.GetAll().Where(c => c.Activo).Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = model.CategoriaIds.Contains(c.Id) }).ToList();
                model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);

                var bcs = _repo.GetBarcodes(p.Id);
                model.Barcodes = bcs.Select(x => x.CodigoBarra).ToList();
                ViewBag.ReturnPage = page ?? 1;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando edición de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar la edición del producto.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Edit(ProductoFormViewModel model, int? page = null)
        {
            try
            {
                model.Sku = model.Sku?.Trim();
                model.Nombre = model.Nombre?.Trim();
                model.UbicacionCodigo = model.UbicacionCodigo?.Trim().ToUpperInvariant();
                model.UnidadMedida = (model.UnidadMedida ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(model.Sku)) { ModelState.AddModelError("Sku", "El SKU es obligatorio."); }
                if (string.IsNullOrWhiteSpace(model.Nombre)) { ModelState.AddModelError("Nombre", "El nombre es obligatorio."); }
                if (string.IsNullOrWhiteSpace(model.UnidadMedida) || !UnidadesPermitidas.Contains(model.UnidadMedida))
                {
                    ModelState.AddModelError("UnidadMedida", "Seleccione una unidad de medida válida.");
                }
                if (model.CategoriaIds == null || model.CategoriaIds.Count == 0)
                {
                    ModelState.AddModelError("CategoriaIds", "Debes seleccionar al menos una categoria.");
                }
                if (!ModelState.IsValid)
                {
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = model.CategoriaIds.Contains(c.Id) }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku, model.Id))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    Response.StatusCode = 409;
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }

                var actual = _repo.GetById(model.Id);
                if (actual == null) return NotFound();
                var hashActual = mi_ferreteria.Security.ConcurrencyToken.ComputeProductoHash(actual, _repo.GetCategorias(actual.Id));
                if (!string.IsNullOrEmpty(model.OriginalHash) && !string.Equals(model.OriginalHash, hashActual, StringComparison.Ordinal))
                {
                    Response.StatusCode = 409;
                    ModelState.AddModelError(string.Empty, "El producto fue modificado por otro proceso. Recarga la página.");
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = model.CategoriaIds.Contains(c.Id) }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }

                var categoriasValidas = _catRepo.GetAll().Where(c => c.Activo).Select(c => c.Id).ToHashSet();
                if (model.CategoriaIds != null && model.CategoriaIds.Any(id2 => !categoriasValidas.Contains(id2)))
                {
                    ModelState.AddModelError("CategoriaIds", "Alguna categoria seleccionada no existe.");
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = model.CategoriaIds.Contains(c.Id) }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
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
                p.UnidadMedida = model.UnidadMedida;
                p.Activo = model.Activo;
                p.UbicacionPreferidaId = model.UbicacionPreferidaId;
                p.UbicacionCodigo = model.UbicacionCodigo;
                _repo.Update(p);

                var catIdsEdit = (model.CategoriaIds ?? new List<long>()).Distinct().Take(3);
                _repo.ReplaceCategorias(p.Id, catIdsEdit);

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
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = model.CategoriaIds.Contains(c.Id) }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    return View(model);
                }
                _repo.ReplaceBarcodes(p.Id, barcodes);
                if (actual != null)
                {
                    RegistrarAuditoria("EDICION",
                        $"Producto #{p.Id}: nombre '{actual.Nombre}' -> '{p.Nombre}', precio {actual.PrecioVentaActual} -> {p.PrecioVentaActual}, activo {actual.Activo} -> {p.Activo}, stockMin {actual.StockMinimo} -> {p.StockMinimo}, unidad '{actual.UnidadMedida}' -> '{p.UnidadMedida}'");
                }
                TempData["Success"] = $"Producto '{p.Nombre}' actualizado correctamente.";
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar producto {ProductoId}", model.Id);
                return Problem("Ocurrió un error al actualizar el producto.");
            }
        }

        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Delete(long id, int? page = null)
        {
            try
            {
                var p = _repo.GetById(id);
                if (p == null) return NotFound();
                ViewBag.ReturnPage = page ?? 1;
                return View(p);
            }
            catch (Exception ex)
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
                var catNames = new List<string>();
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalles de producto {ProductoId}", id);
                return Problem("Ocurrió un error al cargar los detalles del producto.");
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult DeleteConfirmed(long id, int? page = null)
        {
            try
            {
                var prod = _repo.GetById(id);
                var nombre = prod?.Nombre ?? ("#" + id);
                _repo.Delete(id);
                if (prod != null)
                {
                    RegistrarAuditoria("ELIMINADO", $"Producto #{id}: {prod.Nombre} (SKU {prod.Sku})");
                }
                TempData["Success"] = $"Producto '{nombre}' eliminado correctamente.";
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar producto {ProductoId}", id);
                return Problem("Ocurrió un error al eliminar el producto.");
            }
        }

        private List<SelectListItem> BuildUnidadesSelect(string? selected)
        {
            var sel = (selected ?? "unidad").Trim().ToLowerInvariant();
            return UnidadesPermitidas
                .Select(u => new SelectListItem { Value = u, Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(u), Selected = string.Equals(u, sel, StringComparison.OrdinalIgnoreCase) })
                .ToList();
        }

        private void RegistrarAuditoria(string accion, string detalle)
        {
            var userIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var nombre = User?.Identity?.Name ?? "Usuario desconocido";
            if (int.TryParse(userIdClaim, out var uid) && uid > 0)
            {
                _auditoriaRepo.Registrar(uid, nombre, accion.ToUpperInvariant(), detalle);
                HttpContext.Items["AuditLogged"] = true;
            }
        }
    }
}
