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
    public class VentaController : Controller
    {
        private readonly IProductoRepository _productoRepo;
        private readonly IStockRepository _stockRepo;
        private readonly IClienteRepository _clienteRepo;
        private readonly IVentaRepository _ventaRepo;
        private readonly ILogger<VentaController> _logger;

        public VentaController(
            IProductoRepository productoRepo,
            IStockRepository stockRepo,
            IClienteRepository clienteRepo,
            IVentaRepository ventaRepo,
            ILogger<VentaController> logger)
        {
            _productoRepo = productoRepo;
            _stockRepo = stockRepo;
            _clienteRepo = clienteRepo;
            _ventaRepo = ventaRepo;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index(string? q = null, int page = 1)
        {
            try
            {
                _logger.LogInformation("Cargando pantalla de creación de venta");

                const int pageSize = 10;
                if (page < 1) page = 1;

                int total;
                int totalPages;
                IEnumerable<Producto> productos;

                if (!string.IsNullOrWhiteSpace(q))
                {
                    total = _productoRepo.CountSearch(q);
                    totalPages = (int)Math.Ceiling(total / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page > totalPages) page = totalPages;
                    productos = _productoRepo.SearchPageSorted(q, page, pageSize, "nombre_asc").ToList();
                }
                else
                {
                    total = _productoRepo.CountAll();
                    totalPages = (int)Math.Ceiling(total / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page > totalPages) page = totalPages;
                    productos = _productoRepo.GetPageSorted(page, pageSize, "nombre_asc").ToList();
                }

                var stocks = _stockRepo.GetStocks(productos.Select(p => p.Id));
                ViewBag.Productos = productos;
                ViewBag.Stocks = stocks;
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;
                ViewBag.Query = q;

                var model = ConstruirModeloInicial();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en vista Venta");
                return Problem("Ocurrió un error al cargar la vista de venta.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(VentaCrearViewModel model)
        {
            try
            {
                if (model.Lineas == null || model.Lineas.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Debe agregar al menos un producto.");
                }

                var detalles = new List<VentaDetalle>();
                decimal total = 0;
                foreach (var linea in model.Lineas.Where(l => l.Cantidad > 0 && l.ProductoId > 0))
                {
                    var prod = _productoRepo.GetById(linea.ProductoId);
                    if (prod == null)
                    {
                        ModelState.AddModelError(string.Empty, $"El producto con ID {linea.ProductoId} no existe.");
                        continue;
                    }

                    var stock = _stockRepo.GetStock(prod.Id);
                    var permiteSinStock = linea.PermitirVentaSinStock;
                    if (stock < (long)linea.Cantidad && !permiteSinStock)
                    {
                        ModelState.AddModelError(string.Empty, $"Stock insuficiente para {prod.Nombre}. Disponible: {stock}, solicitado: {linea.Cantidad}. Marque 'Permitir sin stock' si corresponde.");
                    }

                    var precio = prod.PrecioVentaActual;
                    var subtotal = precio * linea.Cantidad;
                    total += subtotal;
                    detalles.Add(new VentaDetalle
                    {
                        ProductoId = prod.Id,
                        Descripcion = prod.Nombre,
                        Cantidad = linea.Cantidad,
                        PrecioUnitario = precio,
                        Subtotal = subtotal,
                        PermiteVentaSinStock = permiteSinStock
                    });
                }

                if (detalles.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "La venta no tiene líneas válidas.");
                }

                model.Total = total;

                Cliente? cliente = null;
                if (model.TipoCliente == "REGISTRADO")
                {
                    if (!model.ClienteId.HasValue)
                    {
                        ModelState.AddModelError(nameof(model.ClienteId), "Debe seleccionar un cliente registrado.");
                    }
                    else
                    {
                        cliente = _clienteRepo.GetById(model.ClienteId.Value);
                        if (cliente == null)
                        {
                            ModelState.AddModelError(nameof(model.ClienteId), "El cliente seleccionado no existe.");
                        }
                    }
                }

                if (model.TipoPago == "CUENTA_CORRIENTE")
                {
                    if (cliente == null)
                    {
                        ModelState.AddModelError(string.Empty, "Para cuenta corriente debe seleccionar un cliente registrado.");
                    }
                    else
                    {
                        var saldoActual = _clienteRepo.GetSaldoCuentaCorriente(cliente.Id);
                        var saldoPostVenta = saldoActual + total;
                        if (saldoPostVenta > cliente.LimiteCredito && !model.IgnorarLimiteCredito)
                        {
                            ModelState.AddModelError(string.Empty, $"La venta supera el límite de crédito del cliente ({cliente.LimiteCredito:C}). Debe ser autorizada por el dueño.");
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    // recargar combos y listado de productos por defecto
                    RecargarListas(model);
                    CargarProductosParaVista(null, 1);
                    return View("Index", model);
                }

                // Por ahora usamos un usuario fijo (id=1) ya que no hay autenticación implementada
                var venta = new Venta
                {
                    ClienteId = cliente?.Id,
                    TipoCliente = model.TipoCliente,
                    TipoPago = model.TipoPago,
                    UsuarioId = 1,
                    Estado = "CONFIRMADA"
                };

                var ventaCreada = _ventaRepo.CrearVenta(venta, detalles, registrarFactura: true, cliente: cliente, tipoComprobante: "FACTURA_B");

                if (model.TipoPago == "CUENTA_CORRIENTE" && cliente != null)
                {
                    _clienteRepo.RegistrarDeuda(cliente.Id, ventaCreada.Id, ventaCreada.Total, ventaCreada.UsuarioId, "Venta a cuenta corriente");
                }

                foreach (var d in detalles)
                {
                    try
                    {
                        // Descontar stock y registrar movimiento con motivo "VENTA"
                        if (d.PermiteVentaSinStock)
                        {
                            _stockRepo.EgresarPermitiendoNegativo(d.ProductoId, (long)d.Cantidad, "VENTA");
                        }
                        else
                        {
                            _stockRepo.Egresar(d.ProductoId, (long)d.Cantidad, "VENTA");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al registrar egreso de stock para producto {ProductoId} en venta {VentaId}", d.ProductoId, ventaCreada.Id);
                    }
                }

                TempData["VentaOk"] = $"Venta {ventaCreada.Id} creada correctamente por un total de {ventaCreada.Total:C}.";
                // Redirigir al comprobante para permitir impresión inmediata
                return RedirectToAction(nameof(Comprobante), new { id = ventaCreada.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear venta");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear la venta.");
                RecargarListas(model);
                CargarProductosParaVista(null, 1);
                return View("Index", model);
            }
        }

        [HttpGet]
        public IActionResult Historial(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;

                var total = _ventaRepo.CountAll();
                var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;

                var ventas = _ventaRepo.GetPage(page, pageSize).ToList();

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;

                return View(ventas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar historial de ventas");
                ViewBag.Page = 1;
                ViewBag.PageSize = 10;
                ViewBag.TotalCount = 0;
                ViewBag.TotalPages = 1;
                return View(Enumerable.Empty<Venta>());
            }
        }

        [HttpGet]
        public IActionResult BuscarProductos(string? q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    return Json(Array.Empty<object>());
                }

                const int pageSize = 10;
                var productos = _productoRepo
                    .SearchPageSorted(q, 1, pageSize, "nombre_asc")
                    .ToList();

                var stocks = _stockRepo.GetStocks(productos.Select(p => p.Id))
                             ?? new Dictionary<long, long>();

                var resultado = productos.Select(p =>
                {
                    var stock = stocks.TryGetValue(p.Id, out var s) ? s : 0L;
                    return new
                    {
                        id = p.Id,
                        sku = p.Sku,
                        nombre = p.Nombre,
                        precio = p.PrecioVentaActual,
                        stock
                    };
                });

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en bA-osqueda rA-pida de productos en venta");
                return Json(Array.Empty<object>());
            }
        }

        private void CargarProductosParaVista(string? q, int page)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            int total;
            int totalPages;
            IEnumerable<Producto> productos;

            if (!string.IsNullOrWhiteSpace(q))
            {
                total = _productoRepo.CountSearch(q);
                totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                productos = _productoRepo.SearchPageSorted(q, page, pageSize, "nombre_asc").ToList();
            }
            else
            {
                total = _productoRepo.CountAll();
                totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                productos = _productoRepo.GetPageSorted(page, pageSize, "nombre_asc").ToList();
            }

            var stocks = productos.ToDictionary(p => p.Id, p => _stockRepo.GetStock(p.Id));
            ViewBag.Productos = productos;
            ViewBag.Stocks = stocks;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = total;
            ViewBag.TotalPages = totalPages;
            ViewBag.Query = q;
        }

        private VentaCrearViewModel ConstruirModeloInicial()
        {
            var model = new VentaCrearViewModel
            {
                TipoCliente = "CONSUMIDOR_FINAL",
                TipoPago = "CONTADO",
                IgnorarLimiteCredito = false
            };
            RecargarListas(model);
            return model;
        }

        private void RecargarListas(VentaCrearViewModel model)
        {
            var clientes = _clienteRepo.GetAllActivos();
            model.Clientes = clientes
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Nombre} (límite: {c.LimiteCredito:C})"
                })
                .ToList();
        }

        [HttpGet]
        public IActionResult Comprobante(long id)
        {
            try
            {
                var data = _ventaRepo.ObtenerComprobante(id);
                if (data == null)
                {
                    return NotFound();
                }

                var (venta, detalles, factura) = data.Value;
                var vm = new VentaComprobanteViewModel
                {
                    Venta = venta,
                    Detalles = detalles,
                    Factura = factura
                };
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comprobante de venta {VentaId}", id);
                return Problem("Ocurrió un error al generar el comprobante de venta.");
            }
        }
    }
}
