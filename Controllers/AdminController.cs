using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;

namespace mi_ferreteria.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private readonly IVentaRepository _ventaRepo;
        private readonly IStockRepository _stockRepo;
        private readonly IProductoRepository _productoRepo;
        private readonly IUsuarioRepository _usuarioRepo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly IReporteFinancieroRepository _finanzasRepo;
        private readonly ICategoriaRepository _categoriaRepo;
        private readonly IClienteRepository _clienteRepo;
        private readonly ILogger<AdminController> _logger;
        private static readonly Dictionary<string, string> AuditoriaModulos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLIENTE"] = "Clientes",
            ["PRODUCTO"] = "Productos",
            ["CATEGORIA"] = "Categorías",
            ["USUARIO"] = "Usuarios",
            ["STOCK"] = "Stock",
            ["VENTA"] = "Ventas",
            ["AUTH"] = "Sesiones",
            ["ADMIN"] = "Administración"
        };

        private static readonly Dictionary<string, string> AuditoriaOperaciones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CREATE"] = "Altas",
            ["EDIT"] = "Actualizaciones",
            ["DELETE"] = "Eliminaciones",
            ["ACTIVATE"] = "Reactivaciones",
            ["LOGIN"] = "Inicios de sesión",
            ["LOGOUT"] = "Cierres de sesión",
            ["GENERARNOTADEBITO"] = "Notas de débito CC",
            ["GENERARNOTACUENTACORRIENTE"] = "Notas en cuenta corriente",
            ["REGISTRARPAGOCUENTACORRIENTE"] = "Pagos de cuenta corriente",
            ["INDEX"] = "Movimientos de stock"
        };

        public AdminController(
            IVentaRepository ventaRepo,
            IStockRepository stockRepo,
            IProductoRepository productoRepo,
            IUsuarioRepository usuarioRepo,
            IAuditoriaRepository auditoriaRepo,
            IReporteFinancieroRepository finanzasRepo,
            ICategoriaRepository categoriaRepo,
            IClienteRepository clienteRepo,
            ILogger<AdminController> logger)
        {
            _ventaRepo = ventaRepo;
            _stockRepo = stockRepo;
            _productoRepo = productoRepo;
            _usuarioRepo = usuarioRepo;
            _auditoriaRepo = auditoriaRepo;
            _finanzasRepo = finanzasRepo;
            _categoriaRepo = categoriaRepo;
            _clienteRepo = clienteRepo;
            _logger = logger;
        }

        public IActionResult Dashboard()
        {
            try
            {
                const int topDashboardRows = 5;
                var model = new AdminDashboardViewModel
                {
                    TotalVentas = _ventaRepo.CountAll(),
                    TotalMovimientosStock = _stockRepo.CountMovimientosGlobal(null),
                    TotalMovimientosIngreso = _stockRepo.CountMovimientosGlobal("INGRESO"),
                    TotalMovimientosEgreso = _stockRepo.CountMovimientosGlobal("EGRESO"),
                    TotalProductos = _productoRepo.CountAll(),
                    ProductosInactivos = _productoRepo.CountInactive(),
                    UltimasVentas = _ventaRepo.GetPage(1, topDashboardRows).ToList(),
                    UltimosMovimientosStock = _stockRepo.GetUltimosMovimientos(null, topDashboardRows).ToList(),
                    UltimasAltas = _productoRepo.GetLastCreated(topDashboardRows).ToList(),
                    UltimasBajas = _productoRepo.GetLastInactive(topDashboardRows).ToList(),
                    UltimasEdiciones = _productoRepo.GetLastUpdated(topDashboardRows).ToList()
                };
                var criticosDestacados = _stockRepo.GetProductosStockCritico(null, 1, topDashboardRows, out var totalCriticos).ToList();
                model.ProductosStockCritico = totalCriticos;
                model.ProductosCriticosDestacados = criticosDestacados;

                var productosMapa = new Dictionary<long, string>();
                foreach (var pid in model.UltimosMovimientosStock.Select(m => m.ProductoId).Distinct())
                {
                    var prod = _productoRepo.GetById(pid);
                    if (prod != null)
                    {
                        productosMapa[pid] = prod.Nombre;
                    }
                }
                model.ProductosPorId = productosMapa;

                var usuarios = _usuarioRepo.GetAll();
                model.AdministradoresActivos = usuarios
                    .Where(u => u.Activo && u.Roles.Any(r => string.Equals(r.Nombre, "Administrador", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var alertas = new List<string>();
                if (model.AdministradoresActivos.Count < 2)
                {
                    alertas.Add("Hay menos de 2 administradores activos; considera agregar otro para redundancia y control cruzado.");
                }
                if (model.ProductosInactivos > 0)
                {
                    alertas.Add($"Existen {model.ProductosInactivos} productos inactivos; revisa que correspondan a bajas legitimas.");
                }
                if (model.TotalMovimientosEgreso > model.TotalMovimientosIngreso * 2 && model.TotalMovimientosEgreso > 10)
                {
                    alertas.Add("Los egresos de stock superan ampliamente a los ingresos recientes. Valida posibles faltantes o ajustes no autorizados.");
                }
                model.AlertasSeguridad = alertas;

                ViewData["Title"] = "Panel de administracion";
                return View("Dashboard", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo construir el panel de administracion");
                return Problem("No se pudo cargar el panel de administracion.");
            }
        }

        public IActionResult VentasPendientes()
        {
            try
            {
                var pendientes = _ventaRepo.GetPendientes().ToList();
                var items = new List<VentaPendienteItemViewModel>();
                foreach (var venta in pendientes)
                {
                    Cliente? cliente = null;
                    decimal saldoActual = 0;
                    if (venta.ClienteId.HasValue)
                    {
                        cliente = _clienteRepo.GetById(venta.ClienteId.Value);
                        if (cliente != null)
                        {
                            saldoActual = _clienteRepo.GetSaldoCuentaCorriente(cliente.Id);
                        }
                    }
                    items.Add(new VentaPendienteItemViewModel
                    {
                        Venta = venta,
                        Cliente = cliente,
                        SaldoActual = saldoActual,
                        SaldoPostVenta = saldoActual - venta.Total
                    });
                }
                return View("VentasPendientes", items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar ventas pendientes");
                return Problem("No se pudo cargar el listado de ventas pendientes.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AutorizarVentaPendiente(long ventaId)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                {
                    return Forbid();
                }

                var ventaPendiente = _ventaRepo.GetPendientes().FirstOrDefault(v => v.Id == ventaId);
                if (ventaPendiente == null)
                {
                    TempData["Success"] = "La venta seleccionada ya no esta pendiente.";
                    return RedirectToAction(nameof(VentasPendientes));
                }

                Cliente? cliente = ventaPendiente.ClienteId.HasValue ? _clienteRepo.GetById(ventaPendiente.ClienteId.Value) : null;
                var resultado = _ventaRepo.AutorizarVentaPendiente(
                    ventaId,
                    cliente,
                    "FACTURA_B",
                    registrarFactura: true,
                    registrarPago: true,
                    userId,
                    $"Autorizada por {User.Identity?.Name ?? $"Usuario {userId}"}");

                if (resultado == null)
                {
                    TempData["Success"] = "La venta seleccionada ya no esta pendiente.";
                    return RedirectToAction(nameof(VentasPendientes));
                }

                var (venta, detalles, _) = resultado.Value;
                if (cliente != null)
                {
                    var fechaVencimiento = venta.Fecha.AddDays(30);
                    var saldoActual = _clienteRepo.GetSaldoCuentaCorriente(cliente.Id);
                    var saldoFavor = saldoActual > 0 ? saldoActual : 0m;
                    var aplicadoSaldo = Math.Min(saldoFavor, venta.Total);
                    var deudaCredito = venta.Total - aplicadoSaldo;

                    if (aplicadoSaldo > 0)
                    {
                        var descConsumo = deudaCredito > 0
                            ? $"Aplicación de saldo a favor en venta #{venta.Id} (deuda restante ${deudaCredito:N2})."
                            : $"Venta #{venta.Id} pagada completamente con saldo a favor.";
                        _clienteRepo.RegistrarConsumoSaldo(cliente.Id, venta.Id, aplicadoSaldo, userId, descConsumo);
                    }

                    if (deudaCredito > 0)
                    {
                        var descDeuda = aplicadoSaldo > 0
                            ? $"Venta a cuenta corriente. Saldo a favor aplicado: ${aplicadoSaldo:N2}. Deuda a crédito: ${deudaCredito:N2}."
                            : "Venta a cuenta corriente (crédito).";
                        _clienteRepo.RegistrarDeuda(
                            cliente.Id,
                            venta.Id,
                            deudaCredito,
                            userId,
                            descDeuda,
                            fechaVencimiento);
                    }
                }

                foreach (var detalle in detalles)
                {
                    try
                    {
                        if (detalle.PermiteVentaSinStock)
                        {
                            _stockRepo.EgresarPermitiendoNegativo(detalle.ProductoId, (long)detalle.Cantidad, "VENTA");
                        }
                        else
                        {
                            _stockRepo.Egresar(detalle.ProductoId, (long)detalle.Cantidad, "VENTA");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al descontar stock para producto {ProductoId} en venta {VentaId}", detalle.ProductoId, venta.Id);
                    }
                }

                TempData["Success"] = $"Venta {venta.Id} autorizada correctamente.";
                return RedirectToAction(nameof(VentasPendientes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al autorizar venta pendiente {VentaId}", ventaId);
                TempData["Success"] = "Ocurrió un error al autorizar la venta.";
                return RedirectToAction(nameof(VentasPendientes));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RechazarVentaPendiente(long ventaId, string? motivo)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                {
                    return Forbid();
                }
                var motivoFinal = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
                var ok = _ventaRepo.RechazarVentaPendiente(ventaId, userId, motivoFinal);
                TempData["Success"] = ok
                    ? $"Venta {ventaId} rechazada correctamente."
                    : "La venta seleccionada ya no estaba pendiente.";
                return RedirectToAction(nameof(VentasPendientes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al rechazar venta pendiente {VentaId}", ventaId);
                TempData["Success"] = "Ocurrió un error al rechazar la venta.";
                return RedirectToAction(nameof(VentasPendientes));
            }
        }

        public IActionResult Auditoria(int page = 1, string? search = null, string? modulo = null, string? operacion = null, string? desde = null, string? hasta = null)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                var filtro = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
                var moduloKey = NormalizeModulo(modulo);
                var operacionKey = NormalizeOperacion(operacion);
                var fechaDesde = ParseDate(desde);
                var fechaHasta = ParseDate(hasta);
                if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde > fechaHasta)
                {
                    (fechaDesde, fechaHasta) = (fechaHasta, fechaDesde);
                }
                var fechaHastaExclusive = fechaHasta?.AddDays(1);

                var (registros, total) = _auditoriaRepo.GetPage(page, pageSize, filtro, moduloKey, operacionKey, fechaDesde, fechaHastaExclusive);
                var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages)
                {
                    page = totalPages;
                    (registros, total) = _auditoriaRepo.GetPage(page, pageSize, filtro, moduloKey, operacionKey, fechaDesde, fechaHastaExclusive);
                }

                var model = new AuditoriaListadoViewModel
                {
                    Registros = registros.ToList(),
                    Page = page,
                    TotalPages = totalPages,
                    TotalCount = total,
                    SearchTerm = filtro,
                    SelectedModulo = moduloKey,
                    SelectedModuloLabel = moduloKey != null && AuditoriaModulos.TryGetValue(moduloKey, out var modLbl) ? modLbl : null,
                    SelectedOperacion = operacionKey,
                    SelectedOperacionLabel = operacionKey != null && AuditoriaOperaciones.TryGetValue(operacionKey, out var opLbl) ? opLbl : null,
                    FechaDesde = fechaDesde,
                    FechaHasta = fechaHasta,
                    ModulosDisponibles = AuditoriaModulos
                        .Select(kv => new FiltroOpcion { Value = kv.Key, Label = kv.Value })
                        .OrderBy(o => o.Label)
                        .ToList(),
                    OperacionesDisponibles = AuditoriaOperaciones
                        .Select(kv => new FiltroOpcion { Value = kv.Key, Label = kv.Value })
                        .OrderBy(o => o.Label)
                        .ToList()
                };

                ViewData["Title"] = "Auditoria de usuarios";
                return View("Auditoria", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo cargar la auditoria");
                return Problem("No se pudo cargar la auditoria.");
            }
        }

        public IActionResult Finanzas(int diasTopProductos = 30, int diasTopClientes = 30, long? categoriaTopProductosId = null)
        {
            try
            {
                if (diasTopProductos != 30 && diasTopProductos != 90)
                {
                    diasTopProductos = 30;
                }
                if (diasTopClientes != 30 && diasTopClientes != 90)
                {
                    diasTopClientes = 30;
                }

                if (categoriaTopProductosId.HasValue && categoriaTopProductosId.Value <= 0)
                {
                    categoriaTopProductosId = null;
                }

                var categorias = _categoriaRepo
                    .GetAll()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .ToList();

                if (categoriaTopProductosId.HasValue && categorias.All(c => c.Id != categoriaTopProductosId.Value))
                {
                    categoriaTopProductosId = null;
                }

                var resumen = _finanzasRepo.ObtenerResumen(diasTopProductos, diasTopClientes, categoriaTopProductosId);
                var vm = new FinanzasDashboardViewModel
                {
                    Resumen = resumen,
                    DiasTopProductos = diasTopProductos,
                    DiasTopClientes = diasTopClientes,
                    CategoriaTopProductosId = categoriaTopProductosId,
                    Categorias = categorias
                };
                ViewData["Title"] = "Tablero financiero";
                return View("Finanzas", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo cargar el tablero financiero");
                return Problem("No se pudo cargar el tablero financiero.");
            }
        }

        private static string? NormalizeModulo(string? modulo)
        {
            if (string.IsNullOrWhiteSpace(modulo)) return null;
            var key = modulo.Trim().ToUpperInvariant();
            return AuditoriaModulos.ContainsKey(key) ? key : null;
        }

        private static string? NormalizeOperacion(string? operacion)
        {
            if (string.IsNullOrWhiteSpace(operacion)) return null;
            var key = operacion.Trim().ToUpperInvariant();
            return AuditoriaOperaciones.ContainsKey(key) ? key : null;
        }

        private static DateTimeOffset? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                var local = DateTime.SpecifyKind(dt.Date, DateTimeKind.Local);
                return new DateTimeOffset(local);
            }
            return null;
        }
    }
}
