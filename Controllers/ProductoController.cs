using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using mi_ferreteria.Data;
using mi_ferreteria.Helpers;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace mi_ferreteria.Controllers
{
    public class ProductoController : BaseController
    {
        private readonly IProductoRepository _repo;
        private readonly ICategoriaRepository _catRepo;
        private readonly IStockRepository _stockRepo;
        private readonly ILogger<ProductoController> _logger;

        private static readonly string[] UnidadesPermitidas = ValidationConstants.UnidadesPermitidas;

        public ProductoController(IProductoRepository repo, ICategoriaRepository catRepo, IStockRepository stockRepo, IAuditoriaRepository auditoriaRepo, ILogger<ProductoController> logger)
            : base(auditoriaRepo)
        {
            _repo = repo;
            _catRepo = catRepo;
            _stockRepo = stockRepo;
            _logger = logger;
        }

        // Lista los productos paginados con soporte de búsqueda por texto y ordenamiento por columnas. Incluye alertas de stock crítico.
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
                var alertas = StockAlertHelper.Build(productos, stocks);
                ViewBag.Stocks = stocks;
                ViewBag.StockCriticos = alertas.Criticos;
                ViewBag.StockCriticosDetalle = alertas.Detalles;
                ViewBag.StockCriticosCount = alertas.TotalCriticos;
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
                ViewBag.StockCriticos = new HashSet<long>();
                ViewBag.StockCriticosDetalle = new List<StockCriticoViewModel>();
                ViewBag.StockCriticosCount = 0;
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

        // Muestra el formulario de alta de producto con categorías y unidades de medida disponibles. Soporta carga por modal AJAX.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Create(int? page = null)
        {
            var model = new ProductoFormViewModel { Activo = true, UnidadMedida = "unidad" };
            model.Categorias = _catRepo.GetAll().Where(c => c.Activo).Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
            model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
            ViewBag.ReturnPage = page ?? 1;
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
            return View(model);
        }

        // Valida y persiste el nuevo producto. Verifica SKU único, categorías válidas y códigos de barra no duplicados.
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
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    Response.StatusCode = 409;
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }

                var categoriasValidas = _catRepo.GetAll().Where(c => c.Activo).Select(c => c.Id).ToHashSet();
                if (model.CategoriaIds != null && model.CategoriaIds.Any(id2 => !categoriasValidas.Contains(id2)))
                {
                    ModelState.AddModelError("CategoriaIds", "Alguna categoria seleccionada no existe.");
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
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
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }

                _repo.ReplaceBarcodes(p.Id, barcodes);
                RegistrarAuditoria(nameof(Create), $"Alta de producto #{p.Id}: {p.Nombre} (SKU {p.Sku}, Precio {p.PrecioVentaActual}, Activo={p.Activo})");
                TempData["Success"] = $"Producto '{p.Nombre}' creado correctamente.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto");
                return Problem("Ocurrió un error al crear el producto.");
            }
        }

        // Muestra el formulario de edición con los datos actuales del producto, incluyendo categorías y códigos de barra. Calcula el hash de concurrencia.
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

        // Valida y actualiza el producto. Detecta ediciones simultáneas mediante hash de concurrencia y verifica SKU y códigos de barra únicos.
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
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }
                if (_repo.SkuExists(model.Sku, model.Id))
                {
                    ModelState.AddModelError("Sku", "El SKU ya existe en otro producto.");
                    Response.StatusCode = 409;
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
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
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }

                var categoriasValidas = _catRepo.GetAll().Where(c => c.Activo).Select(c => c.Id).ToHashSet();
                if (model.CategoriaIds != null && model.CategoriaIds.Any(id2 => !categoriasValidas.Contains(id2)))
                {
                    ModelState.AddModelError("CategoriaIds", "Alguna categoria seleccionada no existe.");
                    model.Categorias = _catRepo.GetAll().Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Nombre, Selected = model.CategoriaIds.Contains(c.Id) }).ToList();
                    model.UnidadesMedida = BuildUnidadesSelect(model.UnidadMedida);
                    ViewBag.ReturnPage = page ?? 1;
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }

                var nombreAntes = actual.Nombre;
                var precioAntes = actual.PrecioVentaActual;
                var activoAntes = actual.Activo;
                var stockMinAntes = actual.StockMinimo;
                var unidadAntes = actual.UnidadMedida;

                actual.Sku = model.Sku;
                actual.Nombre = model.Nombre;
                actual.Descripcion = model.Descripcion;
                actual.CategoriaId = (model.CategoriaIds != null && model.CategoriaIds.Count > 0) ? model.CategoriaIds.First() : (long?)null;
                actual.PrecioVentaActual = model.PrecioVentaActual;
                actual.StockMinimo = model.StockMinimo;
                actual.UnidadMedida = model.UnidadMedida;
                actual.Activo = model.Activo;
                actual.UbicacionPreferidaId = model.UbicacionPreferidaId;
                actual.UbicacionCodigo = model.UbicacionCodigo;
                _repo.Update(actual);

                var catIdsEdit = (model.CategoriaIds ?? new List<long>()).Distinct().Take(3);
                _repo.ReplaceCategorias(actual.Id, catIdsEdit);

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
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }
                _repo.ReplaceBarcodes(actual.Id, barcodes);
                RegistrarAuditoria(nameof(Edit),
                    $"Actualizacion de producto #{actual.Id}: nombre '{nombreAntes}' -> '{actual.Nombre}', precio {precioAntes} -> {actual.PrecioVentaActual}, activo {activoAntes} -> {actual.Activo}, stockMin {stockMinAntes} -> {actual.StockMinimo}, unidad '{unidadAntes}' -> '{actual.UnidadMedida}'");
                TempData["Success"] = $"Producto '{actual.Nombre}' actualizado correctamente.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
                return RedirectToAction("Index", new { page = page ?? 1 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar producto {ProductoId}", model.Id);
                return Problem("Ocurrió un error al actualizar el producto.");
            }
        }

        // Muestra la pantalla de confirmación antes de eliminar el producto.
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

        // Muestra el detalle del producto: precio, stock actual, categorías asignadas y códigos de barra registrados.
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

        // Ejecuta la eliminación física del producto y registra la acción en auditoría.
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
                    RegistrarAuditoria("Delete", $"Eliminacion de producto #{id}: {prod.Nombre} (SKU {prod.Sku})");
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

        // Muestra el formulario de importación masiva de productos desde un archivo Excel.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult ImportarExcel()
        {
            return View(new ImportarProductosViewModel());
        }

        // Procesa el archivo Excel fila a fila: crea productos nuevos o actualiza precio y stock de los existentes. Valida encabezados, códigos de barra, SKU y unidades de medida.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult ImportarExcel(ImportarProductosViewModel model)
        {
            if (model.Archivo == null || model.Archivo.Length == 0)
            {
                ModelState.AddModelError("Archivo", "Seleccioná un archivo .xlsx.");
                return View(model);
            }
            if (!model.Archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Archivo", "El archivo debe ser formato .xlsx.");
                return View(model);
            }

            var resultado = new ImportarProductosResultadoViewModel();
            var todasCategorias = _catRepo.GetAll().Where(c => c.Activo).ToDictionary(c => c.Nombre.Trim().ToLowerInvariant(), c => c.Id);

            try
            {
                using var stream = model.Archivo.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheet(1);

                // Validar encabezados
                var encabezadosEsperados = new[] { "nombre", "codigobarra", "precio", "cantidad", "sku", "descripcion", "categoria", "stockminimo", "unidadmedida", "ubicacion" };
                var erroresEncabezado = new List<string>();
                for (int col = 1; col <= encabezadosEsperados.Length; col++)
                {
                    var valorCelda = ws.Cell(1, col).GetString().Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
                    if (valorCelda != encabezadosEsperados[col - 1])
                        erroresEncabezado.Add($"Columna {col}: se esperaba '{encabezadosEsperados[col - 1]}', se encontró '{ws.Cell(1, col).GetString().Trim()}'");
                }
                if (erroresEncabezado.Any())
                {
                    var msg = "El archivo no tiene el formato esperado. Revisá los encabezados:\n" + string.Join("\n", erroresEncabezado);
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return BadRequest(new { error = msg });
                    ModelState.AddModelError("Archivo", msg);
                    return View(model);
                }

                var filas = ws.RowsUsed().Skip(1).ToList();

                foreach (var fila in filas)
                {
                    int numFila = fila.RowNumber();
                    string nombre = fila.Cell(1).GetString().Trim();
                    string codigoBarra = fila.Cell(2).GetString().Trim();
                    string precioRaw = fila.Cell(3).GetString().Trim();
                    string cantidadRaw = fila.Cell(4).GetString().Trim();
                    string skuRaw = fila.Cell(5).GetString().Trim();
                    string descripcion = fila.Cell(6).GetString().Trim();
                    string categoriaNombre = fila.Cell(7).GetString().Trim();
                    string stockMinimoRaw = fila.Cell(8).GetString().Trim();
                    string unidadMedida = fila.Cell(9).GetString().Trim().ToLowerInvariant();
                    string ubicacion = fila.Cell(10).GetString().Trim().ToUpperInvariant();

                    // Validaciones básicas
                    if (string.IsNullOrWhiteSpace(nombre))
                    {
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, Estado = "error", Mensaje = "El nombre es obligatorio." });
                        resultado.TotalErrores++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(codigoBarra))
                    {
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, Nombre = nombre, Estado = "error", Mensaje = "El código de barra es obligatorio." });
                        resultado.TotalErrores++;
                        continue;
                    }
                    if (!decimal.TryParse(precioRaw.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precio) || precio < 0)
                    {
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, Nombre = nombre, CodigoBarra = codigoBarra, Estado = "error", Mensaje = "Precio inválido." });
                        resultado.TotalErrores++;
                        continue;
                    }
                    if (!long.TryParse(cantidadRaw, out long cantidad) || cantidad < 0)
                    {
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, Nombre = nombre, CodigoBarra = codigoBarra, Estado = "error", Mensaje = "Cantidad inválida." });
                        resultado.TotalErrores++;
                        continue;
                    }

                    // Unidad de medida: si viene vacía o inválida, usar "unidad"
                    if (string.IsNullOrWhiteSpace(unidadMedida) || !UnidadesPermitidas.Contains(unidadMedida))
                        unidadMedida = "unidad";

                    // Stock mínimo
                    int stockMinimo = 0;
                    if (!string.IsNullOrWhiteSpace(stockMinimoRaw))
                        int.TryParse(stockMinimoRaw, out stockMinimo);

                    // Categoría por nombre
                    long? categoriaId = null;
                    if (!string.IsNullOrWhiteSpace(categoriaNombre) && todasCategorias.TryGetValue(categoriaNombre.ToLowerInvariant(), out var cid))
                        categoriaId = cid;

                    // Ubicación: validar formato letra+número
                    string? ubicacionFinal = null;
                    if (!string.IsNullOrWhiteSpace(ubicacion) && System.Text.RegularExpressions.Regex.IsMatch(ubicacion, @"^[A-Z][0-9]{1,2}$"))
                        ubicacionFinal = ubicacion;

                    // Buscar producto existente: primero por código de barra, luego por SKU
                    Producto? existente = _repo.GetByBarcode(codigoBarra);
                    if (existente == null && !string.IsNullOrWhiteSpace(skuRaw))
                        existente = _repo.GetBySku(skuRaw);

                    try
                    {
                        if (existente != null)
                        {
                            // Actualizar precio y otros campos opcionales
                            existente.PrecioVentaActual = precio;
                            existente.Nombre = nombre;
                            if (!string.IsNullOrWhiteSpace(descripcion)) existente.Descripcion = descripcion;
                            if (categoriaId.HasValue) existente.CategoriaId = categoriaId;
                            if (stockMinimo > 0) existente.StockMinimo = stockMinimo;
                            if (!string.IsNullOrWhiteSpace(ubicacionFinal)) existente.UbicacionCodigo = ubicacionFinal;
                            existente.UnidadMedida = unidadMedida;
                            _repo.Update(existente);

                            // Agregar código de barra si no existe aún en este producto
                            if (!_repo.BarcodeExists(codigoBarra, existente.Id))
                            {
                                var bcs = _repo.GetBarcodes(existente.Id).ToList();
                                bcs.Add(new ProductoCodigoBarra { CodigoBarra = codigoBarra });
                                _repo.ReplaceBarcodes(existente.Id, bcs);
                            }

                            // Registrar ingreso de stock si la cantidad es mayor a 0
                            if (cantidad > 0)
                                _stockRepo.Ingresar(existente.Id, cantidad, "Importación desde Excel", precio > 0 ? precio : (decimal?)null);

                            resultado.Resultados.Add(new ImportarProductoResultado
                            {
                                Fila = numFila, Nombre = nombre, CodigoBarra = codigoBarra,
                                Estado = "actualizado", Mensaje = cantidad > 0 ? $"Precio actualizado. Stock +{cantidad}." : "Precio actualizado."
                            });
                            resultado.TotalActualizados++;
                        }
                        else
                        {
                            // Crear nuevo producto
                            string sku = !string.IsNullOrWhiteSpace(skuRaw) ? skuRaw : GenerarSku(nombre);
                            if (_repo.SkuExists(sku))
                                sku = sku + "-" + DateTime.Now.Ticks.ToString()[^4..];

                            var nuevo = new Producto
                            {
                                Sku = sku,
                                Nombre = nombre,
                                Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion,
                                CategoriaId = categoriaId,
                                PrecioVentaActual = precio,
                                StockMinimo = stockMinimo,
                                UnidadMedida = unidadMedida,
                                Activo = true,
                                UbicacionCodigo = ubicacionFinal
                            };
                            _repo.Add(nuevo);
                            if (categoriaId.HasValue)
                                _repo.ReplaceCategorias(nuevo.Id, new[] { categoriaId.Value });
                            _repo.ReplaceBarcodes(nuevo.Id, new[] { new ProductoCodigoBarra { CodigoBarra = codigoBarra } });

                            if (cantidad > 0)
                                _stockRepo.Ingresar(nuevo.Id, cantidad, "Importación desde Excel", precio > 0 ? precio : (decimal?)null);

                            resultado.Resultados.Add(new ImportarProductoResultado
                            {
                                Fila = numFila, Nombre = nombre, CodigoBarra = codigoBarra,
                                Estado = "creado", Mensaje = cantidad > 0 ? $"Producto creado. Stock inicial: {cantidad}." : "Producto creado sin stock."
                            });
                            resultado.TotalCreados++;
                        }
                    }
                    catch (Exception exFila)
                    {
                        _logger.LogError(exFila, "Error procesando fila {Fila} de importación Excel", numFila);
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, Nombre = nombre, CodigoBarra = codigoBarra, Estado = "error", Mensaje = "Error interno al procesar la fila." });
                        resultado.TotalErrores++;
                    }
                }

                RegistrarAuditoria("ImportarExcel", $"Importación Excel: {resultado.TotalCreados} creados, {resultado.TotalActualizados} actualizados, {resultado.TotalErrores} errores.");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return PartialView("_ImportarExcelResultado", resultado);
                return View("ImportarExcelResultado", resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al importar Excel de productos");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { error = "El archivo no pudo procesarse. Verificá que sea un .xlsx válido." });
                ModelState.AddModelError("Archivo", "El archivo no pudo procesarse. Verificá que sea un .xlsx válido.");
                return View(model);
            }
        }

        // Actualiza el precio de costo de productos existentes a partir de un Excel de lista de precios de proveedor. Busca por código de barra o SKU.
        [Authorize(Roles = "Administrador,Stock")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ImportarListaPrecios(ImportarProductosViewModel model)
        {
            if (model.Archivo == null || model.Archivo.Length == 0)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { error = "Seleccioná un archivo .xlsx." });
                return RedirectToAction("Index");
            }
            if (!model.Archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { error = "El archivo debe ser formato .xlsx." });
                return RedirectToAction("Index");
            }

            var resultado = new ImportarProductosResultadoViewModel();

            try
            {
                using var stream = model.Archivo.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheet(1);

                // Validar encabezados
                var encabezadosEsperadosLP = new[] { "codigobarra_o_sku", "preciocompra" };
                var erroresEncabezadoLP = new List<string>();
                for (int col = 1; col <= encabezadosEsperadosLP.Length; col++)
                {
                    var valorCelda = ws.Cell(1, col).GetString().Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
                    var esperado = encabezadosEsperadosLP[col - 1].Replace("_", "");
                    if (valorCelda != esperado)
                        erroresEncabezadoLP.Add($"Columna {col}: se esperaba '{encabezadosEsperadosLP[col - 1]}', se encontró '{ws.Cell(1, col).GetString().Trim()}'");
                }
                if (erroresEncabezadoLP.Any())
                {
                    var msg = "El archivo no tiene el formato esperado. Revisá los encabezados:\n" + string.Join("\n", erroresEncabezadoLP);
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return BadRequest(new { error = msg });
                    return RedirectToAction("Index");
                }

                var filas = ws.RowsUsed().Skip(1).ToList();

                foreach (var fila in filas)
                {
                    int numFila = fila.RowNumber();
                    string codigoRaw = fila.Cell(1).GetString().Trim(); // CodigoBarra o SKU
                    string precioRaw = fila.Cell(2).GetString().Trim();
                    string nombreRef = fila.Cell(3).GetString().Trim();  // opcional, solo referencia

                    if (string.IsNullOrWhiteSpace(codigoRaw))
                    {
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, Estado = "error", Mensaje = "El código es obligatorio." });
                        resultado.TotalErrores++;
                        continue;
                    }
                    if (!decimal.TryParse(precioRaw.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precio) || precio < 0)
                    {
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, CodigoBarra = codigoRaw, Nombre = nombreRef, Estado = "error", Mensaje = "Precio inválido." });
                        resultado.TotalErrores++;
                        continue;
                    }

                    try
                    {
                        // Buscar por código de barra primero, luego por SKU
                        Producto? producto = _repo.GetByBarcode(codigoRaw);
                        if (producto == null)
                            producto = _repo.GetBySku(codigoRaw);

                        if (producto != null)
                        {
                            _repo.ActualizarPrecioCosto(producto.Id, precio);
                            resultado.Resultados.Add(new ImportarProductoResultado
                            {
                                Fila = numFila,
                                Nombre = producto.Nombre,
                                CodigoBarra = codigoRaw,
                                Estado = "actualizado",
                                Mensaje = $"Precio de costo actualizado a ${precio:N2}."
                            });
                            resultado.TotalActualizados++;
                        }
                        else
                        {
                            resultado.Resultados.Add(new ImportarProductoResultado
                            {
                                Fila = numFila,
                                Nombre = nombreRef,
                                CodigoBarra = codigoRaw,
                                Estado = "no_encontrado",
                                Mensaje = "No se encontró ningún producto con ese código o SKU."
                            });
                            resultado.TotalNoEncontrados++;
                        }
                    }
                    catch (Exception exFila)
                    {
                        _logger.LogError(exFila, "Error procesando fila {Fila} de lista de precios", numFila);
                        resultado.Resultados.Add(new ImportarProductoResultado { Fila = numFila, CodigoBarra = codigoRaw, Estado = "error", Mensaje = "Error interno al procesar la fila." });
                        resultado.TotalErrores++;
                    }
                }

                RegistrarAuditoria("ImportarListaPrecios", $"Importación lista de precios: {resultado.TotalActualizados} actualizados, {resultado.TotalCreados} no encontrados, {resultado.TotalErrores} errores.");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return PartialView("_ImportarListaPreciosResultado", resultado);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al importar lista de precios");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { error = "El archivo no pudo procesarse. Verificá que sea un .xlsx válido." });
                return RedirectToAction("Index");
            }
        }

        // Genera y descarga un archivo Excel de plantilla con encabezados y filas de ejemplo para la importación masiva de productos.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult DescargarPlantillaImportacion()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Productos");

            // Encabezados
            var headers = new[] { "Nombre", "CodigoBarra", "Precio", "Cantidad", "SKU", "Descripcion", "Categoria", "StockMinimo", "UnidadMedida", "Ubicacion" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Filas de ejemplo
            var filas = new object[,]
            {
                { "Martillo carpintero 500g",  "7790001000011", 3500.00m,  10, "MAR-500",  "Martillo de carpintero con mango de madera",    "Herramientas",   2, "unidad",  "A1" },
                { "Pintura látex blanco 10L",   "7790001000022", 8900.50m,   5, "PIN-LAT10","Pintura látex interior/exterior blanco",         "Pinturas",       1, "litro",   "B3" },
                { "Tornillo hexagonal 1/4 x1\"","7790001000033",   12.00m, 500, "TOR-HEX14","Tornillo hexagonal zincado 1/4 pulgada",         "Ferretería",     50, "unidad", "C2" },
                { "Caño PVC 110mm x 3m",        "7790001000044",  650.00m,  20, "CAN-PVC11","Caño PVC sanitario 110mm diámetro 3 metros",     "Plomería",        3, "unidad",  "D1" },
                { "Llave inglesa 12\"",          "7790001000055", 4200.00m,   8, "",         "Llave inglesa regulable 12 pulgadas",            "",               1, "unidad",  ""   },
            };

            for (int r = 0; r < filas.GetLength(0); r++)
            {
                for (int c = 0; c < filas.GetLength(1); c++)
                    ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(filas[r, c]);
                // Alternar color de fila
                if (r % 2 == 0)
                    ws.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
            }

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 20;

            using var ms = new System.IO.MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla_importar_productos.xlsx");
        }

        // Genera y descarga un archivo Excel de plantilla para actualización de precios de costo desde lista de proveedor.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult DescargarPlantillaListaPrecios()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Lista de Precios");

            // Encabezados
            var headers = new[] { "CodigoBarra_o_SKU", "PrecioCompra", "Nombre (referencia)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Filas de ejemplo — usar los mismos códigos que la plantilla de importación
            var filas = new object[,]
            {
                { "7790001000011", 1800.00m, "Martillo carpintero 500g"   },
                { "7790001000022", 4200.00m, "Pintura látex blanco 10L"   },
                { "7790001000033",    5.50m, "Tornillo hexagonal 1/4 x1\""},
                { "7790001000044",  310.00m, "Caño PVC 110mm x 3m"        },
                { "MAR-500",       1750.00m, "Martillo (búsqueda por SKU)" },
            };

            for (int r = 0; r < filas.GetLength(0); r++)
            {
                for (int c = 0; c < filas.GetLength(1); c++)
                    ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(filas[r, c]);
                if (r % 2 == 0)
                    ws.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
            }

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 20;

            using var ms = new System.IO.MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla_lista_precios_proveedor.xlsx");
        }

        private static string GenerarSku(string nombre)
        {
            var palabras = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var base_ = string.Concat(palabras.Take(3).Select(p => p.Length >= 3 ? p[..3] : p)).ToUpperInvariant();
            base_ = System.Text.RegularExpressions.Regex.Replace(base_, "[^A-Z0-9]", "");
            if (base_.Length < 3) base_ = (base_ + "PROD").Substring(0, 4);
            return base_ + "-" + DateTime.Now.Ticks.ToString()[^5..];
        }

        private List<SelectListItem> BuildUnidadesSelect(string? selected)
        {
            var sel = (selected ?? "unidad").Trim().ToLowerInvariant();
            return UnidadesPermitidas
                .Select(u => new SelectListItem { Value = u, Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(u), Selected = string.Equals(u, sel, StringComparison.OrdinalIgnoreCase) })
                .ToList();
        }

    }
}
