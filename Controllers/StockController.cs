using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;

namespace mi_ferreteria.Controllers
{
    public class StockController : Controller
    {
        private readonly ILogger<StockController> _logger;
        private readonly IStockRepository _stockRepo;
        private readonly IProductoRepository _prodRepo;
        private readonly IAuditoriaRepository _auditoriaRepo;

        public StockController(ILogger<StockController> logger, IStockRepository stockRepo, IProductoRepository prodRepo, IAuditoriaRepository auditoriaRepo)
        {
            _logger = logger;
            _stockRepo = stockRepo;
            _prodRepo = prodRepo;
            _auditoriaRepo = auditoriaRepo;
        }

        [HttpGet]
        public IActionResult Index(string? q = null, int page = 1)
        {
            try
            {
                if (!PuedeGestionarStock()) return Forbid();

                ViewData["Title"] = "Gestion de Stock";
                PrepararListadoProductos(q, page);
                return View(new StockCargaViewModel());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en vista Stock");
                return Problem("Ocurrio un error al cargar la vista de stock.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cargar(StockCargaViewModel model, string? q = null, int page = 1)
        {
            try
            {
                if (!PuedeGestionarStock()) return Forbid();
                model ??= new StockCargaViewModel();

                var lineasParaMostrar = new List<StockCargaLineaViewModel>();
                var lineasValidas = new List<StockCargaLineaViewModel>();
                var tipo = string.IsNullOrWhiteSpace(model.TipoMovimiento)
                    ? "INGRESO"
                    : model.TipoMovimiento.Trim().ToUpperInvariant();
                var esIngreso = tipo == "INGRESO";
                var motivo = string.IsNullOrWhiteSpace(model.Motivo) ? (esIngreso ? "Carga masiva" : "Egreso manual") : model.Motivo.Trim();
                model.TipoMovimiento = esIngreso ? "INGRESO" : "EGRESO";

                if (model.Lineas == null || model.Lineas.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, esIngreso
                        ? "Debe seleccionar al menos un producto para ingresar stock."
                        : "Debe seleccionar al menos un producto para egresar stock.");
                }

                foreach (var linea in model.Lineas ?? new List<StockCargaLineaViewModel>())
                {
                    if (linea.ProductoId <= 0)
                    {
                        ModelState.AddModelError(string.Empty, "Hay un producto invalido en la lista.");
                        continue;
                    }

                    var prod = _prodRepo.GetById(linea.ProductoId);
                    if (prod == null)
                    {
                        ModelState.AddModelError(string.Empty, $"El producto con ID {linea.ProductoId} no existe.");
                        continue;
                    }

                    var stockActual = _stockRepo.GetStock(prod.Id);
                    var normalizada = new StockCargaLineaViewModel
                    {
                        ProductoId = prod.Id,
                        ProductoNombre = prod.Nombre,
                        UnidadMedida = prod.UnidadMedida,
                        Cantidad = linea.Cantidad,
                        PrecioCompra = linea.PrecioCompra,
                        StockActual = stockActual
                    };
                    lineasParaMostrar.Add(normalizada);

                    if (linea.Cantidad <= 0)
                    {
                        ModelState.AddModelError(string.Empty, $"La cantidad para {prod.Nombre} debe ser mayor a 0.");
                        continue;
                    }
                    if (esIngreso && linea.PrecioCompra.HasValue && linea.PrecioCompra.Value < 0)
                    {
                        ModelState.AddModelError(string.Empty, $"El precio de compra de {prod.Nombre} no puede ser negativo.");
                        continue;
                    }
                    if (esIngreso && linea.PrecioCompra.HasValue && linea.PrecioCompra.Value >= prod.PrecioVentaActual)
                    {
                        ModelState.AddModelError(string.Empty, $"Advertencia: el precio de compra para {prod.Nombre} ({linea.PrecioCompra:C}) es igual o mayor que el precio de venta actual ({prod.PrecioVentaActual:C}).");
                    }
                    if (!esIngreso && linea.Cantidad > stockActual)
                    {
                        ModelState.AddModelError(string.Empty, $"No hay stock suficiente de {prod.Nombre} para egresar {linea.Cantidad}. Disponible: {stockActual}.");
                        continue;
                    }
                    if (!esIngreso)
                    {
                        normalizada.PrecioCompra = null;
                    }

                    lineasValidas.Add(normalizada);
                }

                if (!ModelState.IsValid)
                {
                    model.Lineas = lineasParaMostrar;
                    PrepararListadoProductos(q, page);
                    return View("Index", model);
                }

                foreach (var linea in lineasValidas)
                {
                    if (esIngreso)
                    {
                        _stockRepo.Ingresar(linea.ProductoId, linea.Cantidad, motivo, linea.PrecioCompra);
                        RegistrarAuditoria("INGRESO_STOCK", $"Ingreso de {linea.Cantidad} {linea.UnidadMedida} de {linea.ProductoNombre} (ID {linea.ProductoId})");
                    }
                    else
                    {
                        _stockRepo.Egresar(linea.ProductoId, linea.Cantidad, motivo);
                        RegistrarAuditoria("EGRESO_STOCK", $"Egreso de {linea.Cantidad} {linea.UnidadMedida} de {linea.ProductoNombre} (ID {linea.ProductoId})");
                    }
                }

                TempData["StockOk"] = esIngreso
                    ? $"Se registraron {lineasValidas.Count} ingresos de stock."
                    : $"Se registraron {lineasValidas.Count} egresos de stock.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar stock masivo");
                TempData["StockError"] = "Ocurrio un error al registrar el stock.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public IActionResult BuscarProductos(string? q)
        {
            try
            {
                if (!PuedeGestionarStock()) return Forbid();
                if (string.IsNullOrWhiteSpace(q))
                {
                    return Json(Array.Empty<object>());
                }

                const int pageSize = 10;
                var productos = _prodRepo
                    .SearchPageSorted(q, 1, pageSize, "nombre_asc")
                    .ToList();

                var stocks = _stockRepo.GetStocks(productos.Select(p => p.Id))
                             ?? new Dictionary<long, long>();

                var resultado = productos.Select(p =>
                {
                    var stock = stocks.TryGetValue(p.Id, out var s) ? s : 0L;
                    var pc = _stockRepo.GetUltimoPrecioCompra(p.Id);
                    return new
                    {
                        id = p.Id,
                        sku = p.Sku,
                        nombre = p.Nombre,
                        precioVenta = p.PrecioVentaActual,
                        precioCompra = pc,
                        unidad = p.UnidadMedida,
                        stock
                    };
                });

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en busqueda rapida de productos para stock");
                return Json(Array.Empty<object>());
            }
        }

        [HttpGet]
        public IActionResult Buscar(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return RedirectToAction(nameof(Index));
            try
            {
                var matches = _prodRepo.SearchPageSorted(q, 1, 20, "nombre_asc").ToList();
                if (matches == null || matches.Count == 0)
                {
                    var enc = System.Net.WebUtility.UrlEncode($"No se encontraron productos para '{q}'.");
                    return Redirect(Url.Action("Index", "Stock") + "?smsg=" + enc);
                }
                var prod = matches.First();
                return RedirectToAction("Manage", "StockProducto", new { id = prod.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en busqueda de stock por producto: {Query}", q);
                var enc = System.Net.WebUtility.UrlEncode("Ocurrio un error al buscar el producto.");
                return Redirect(Url.Action("Index", "Stock") + "?smsg=" + enc);
            }
        }

        [HttpGet]
        public IActionResult Movimientos(int page = 1)
        {
            try
            {
                if (!PuedeGestionarStock()) return Forbid();
                const int pageSize = 10;
                if (page < 1) page = 1;

                var total = _stockRepo.CountMovimientosGlobal(null);
                var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;

                var movimientos = _stockRepo.GetMovimientosGlobalPage(null, page, pageSize).ToList();
                var ids = movimientos.Select(x => x.ProductoId).Distinct().ToList();
                var names = new Dictionary<long, string>();
                var units = new Dictionary<long, string>();
                foreach (var pid in ids)
                {
                    var p = _prodRepo.GetById(pid);
                    names[pid] = p?.Nombre ?? ("#" + pid);
                    units[pid] = p?.UnidadMedida ?? "unidad";
                }

                ViewBag.Movimientos = movimientos;
                ViewBag.ProductNames = names;
                ViewBag.ProductUnits = units;
                ViewBag.Page = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = total;
                return View("Movimientos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar movimientos de stock");
                return Problem("Ocurrio un error al cargar los movimientos de stock.");
            }
        }

        private void PrepararListadoProductos(string? q, int page)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            int total;
            int totalPages;
            IEnumerable<Producto> productos;

            if (!string.IsNullOrWhiteSpace(q))
            {
                total = _prodRepo.CountSearch(q);
                totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                productos = _prodRepo.SearchPageSorted(q, page, pageSize, "nombre_asc").ToList();
            }
            else
            {
                total = _prodRepo.CountAll();
                totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                productos = _prodRepo.GetPageSorted(page, pageSize, "nombre_asc").ToList();
            }

            var stocks = _stockRepo.GetStocks(productos.Select(p => p.Id));
            var preciosCompra = new Dictionary<long, decimal?>();
            foreach (var p in productos)
            {
                preciosCompra[p.Id] = _stockRepo.GetUltimoPrecioCompra(p.Id);
            }
            ViewBag.Productos = productos;
            ViewBag.Stocks = stocks;
            ViewBag.PreciosCompra = preciosCompra;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = total;
            ViewBag.TotalPages = totalPages;
            ViewBag.Query = q ?? string.Empty;
        }

        private bool PuedeGestionarStock()
        {
            return User.IsInRole("Administrador") || User.IsInRole("Stock");
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
