using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using ClosedXML.Excel;
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

        // Panel principal: muestra KPIs de ventas, stock, productos y alertas de seguridad.
        public IActionResult Dashboard()
        {
            try
            {
                const int topDashboardRows = 5;
                var stockResumen = _stockRepo.CountMovimientosResumen();
                var model = new AdminDashboardViewModel
                {
                    TotalVentas = _ventaRepo.CountAll(),
                    TotalMovimientosStock = stockResumen.Total,
                    TotalMovimientosIngreso = stockResumen.Ingreso,
                    TotalMovimientosEgreso = stockResumen.Egreso,
                    TotalProductos = _productoRepo.CountAll(),
                    ProductosInactivos = _productoRepo.CountInactive(),
                    UltimasVentas = _ventaRepo.GetPage(1, topDashboardRows).ToList(),
                    UltimosMovimientosStock = _stockRepo.GetUltimosMovimientos(null, topDashboardRows).ToList()
                };
                var criticosDestacados = _stockRepo.GetProductosStockCritico(null, 1, topDashboardRows, out var totalCriticos).ToList();
                model.ProductosStockCritico = totalCriticos;
                model.ProductosCriticosDestacados = criticosDestacados;

                var pids = model.UltimosMovimientosStock.Select(m => m.ProductoId).Distinct();
                model.ProductosPorId = _productoRepo.GetNombresPorIds(pids).ToDictionary(k => k.Key, v => v.Value);

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

        // Lista las ventas en estado PENDIENTE_AUTORIZACION con el saldo actual y proyectado de cada cliente.
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
        // Confirma una venta pendiente: cambia el estado, genera la factura, registra el pago y descuenta el stock.
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
                            ? $"Venta #{venta.Id} pagada parcialmente con saldo a favor. Deuda restante: ${deudaCredito:N2}."
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
        // Rechaza una venta pendiente, guarda el motivo en observaciones y la deja como registro histórico sin efectos.
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

        // Muestra el registro de auditoría paginado, con filtros por texto, módulo, tipo de operación y rango de fechas.
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

        // Tablero financiero: totales de ventas por período, margen bruto, top productos, top clientes y deudores.
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

        // ── REPORTES ──────────────────────────────────────────────────────────────

        // Vista de exportación: punto de entrada para descargar reportes en Excel o verlos en formato imprimible.
        public IActionResult Reportes()
        {
            ViewData["Title"] = "Exportar Reportes";
            return View();
        }

        // ── Excel exports ─────────────────────────────────────────────────────────

        // Genera un .xlsx con 4 hojas: resumen general, top productos, top clientes y clientes deudores.
        public IActionResult ExportarFinanzasExcel()
        {
            var resumen = _finanzasRepo.ObtenerResumen();
            using var wb = new XLWorkbook();

            // Hoja 1: Resumen
            var ws1 = wb.Worksheets.Add("Resumen General");
            ws1.Cell(1, 1).Value = "Indicador"; ws1.Cell(1, 2).Value = "Valor";
            ws1.Row(1).Style.Font.Bold = true;
            ws1.Cell(2, 1).Value = "Total vendido (histórico)"; ws1.Cell(2, 2).Value = (double)resumen.TotalVendido;
            ws1.Cell(3, 1).Value = "Ventas del día";             ws1.Cell(3, 2).Value = (double)resumen.VentasDia;
            ws1.Cell(4, 1).Value = "Ventas de la semana";        ws1.Cell(4, 2).Value = (double)resumen.VentasSemana;
            ws1.Cell(5, 1).Value = "Ventas del mes";             ws1.Cell(5, 2).Value = (double)resumen.VentasMes;
            ws1.Cell(6, 1).Value = "Ventas del año";             ws1.Cell(6, 2).Value = (double)resumen.VentasAnio;
            ws1.Cell(7, 1).Value = "Margen bruto (%)";           ws1.Cell(7, 2).Value = (double)resumen.MargenBrutoPorcentaje;
            ws1.Cell(8, 1).Value = "Promedio días cobro CC";     ws1.Cell(8, 2).Value = resumen.PromedioCobroDias.HasValue ? (double)resumen.PromedioCobroDias.Value : 0;
            ws1.Cell(9, 1).Value = "Deuda total clientes CC";    ws1.Cell(9, 2).Value = (double)resumen.DeudaTotal;
            for (int r = 2; r <= 9; r++)
            {
                ws1.Cell(r, 2).Style.NumberFormat.Format = "#,##0.00";
            }
            ws1.Columns().AdjustToContents();

            // Hoja 2: Top Productos
            var ws2 = wb.Worksheets.Add("Top Productos");
            ws2.Cell(1, 1).Value = "Producto"; ws2.Cell(1, 2).Value = "Cantidad vendida"; ws2.Cell(1, 3).Value = "Importe total";
            ws2.Row(1).Style.Font.Bold = true;
            for (int i = 0; i < resumen.TopProductos.Count; i++)
            {
                var tp = resumen.TopProductos[i];
                ws2.Cell(i + 2, 1).Value = tp.Nombre;
                ws2.Cell(i + 2, 2).Value = (double)tp.CantidadVendida;
                ws2.Cell(i + 2, 3).Value = (double)tp.ImporteTotal;
                ws2.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            ws2.Columns().AdjustToContents();

            // Hoja 3: Top Clientes
            var ws3 = wb.Worksheets.Add("Top Clientes");
            ws3.Cell(1, 1).Value = "Cliente"; ws3.Cell(1, 2).Value = "Compras"; ws3.Cell(1, 3).Value = "Importe total";
            ws3.Row(1).Style.Font.Bold = true;
            for (int i = 0; i < resumen.TopClientes.Count; i++)
            {
                var tc = resumen.TopClientes[i];
                ws3.Cell(i + 2, 1).Value = tc.Nombre;
                ws3.Cell(i + 2, 2).Value = tc.CantidadCompras;
                ws3.Cell(i + 2, 3).Value = (double)tc.ImporteTotal;
                ws3.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            ws3.Columns().AdjustToContents();

            // Hoja 4: Deudores
            var ws4 = wb.Worksheets.Add("Clientes Deudores");
            ws4.Cell(1, 1).Value = "Cliente"; ws4.Cell(1, 2).Value = "Deuda total"; ws4.Cell(1, 3).Value = "Límite crédito"; ws4.Cell(1, 4).Value = "% Uso";
            ws4.Row(1).Style.Font.Bold = true;
            for (int i = 0; i < resumen.Deudores.Count; i++)
            {
                var d = resumen.Deudores[i];
                ws4.Cell(i + 2, 1).Value = d.Nombre;
                ws4.Cell(i + 2, 2).Value = (double)d.DeudaTotal;
                ws4.Cell(i + 2, 3).Value = (double)d.LimiteCredito;
                var pct = d.LimiteCredito > 0 ? Math.Round((double)(d.DeudaTotal / d.LimiteCredito * 100), 1) : 0;
                ws4.Cell(i + 2, 4).Value = pct;
                ws4.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
                ws4.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            ws4.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"finanzas_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // Exporta el historial de ventas filtrado por rango de fechas a un archivo Excel.
        public IActionResult ExportarHistorialExcel(string? desde, string? hasta)
        {
            var fechaDesde = ParseDate(desde)?.DateTime;
            var fechaHasta = ParseDate(hasta)?.DateTime;
            var ventas = _ventaRepo.GetParaExportar(fechaDesde, fechaHasta).ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Historial de Ventas");
            ws.Cell(1, 1).Value = "ID";
            ws.Cell(1, 2).Value = "Fecha";
            ws.Cell(1, 3).Value = "Tipo cliente";
            ws.Cell(1, 4).Value = "Tipo pago";
            ws.Cell(1, 5).Value = "Total";
            ws.Cell(1, 6).Value = "Estado";
            ws.Row(1).Style.Font.Bold = true;
            for (int i = 0; i < ventas.Count; i++)
            {
                var v = ventas[i];
                ws.Cell(i + 2, 1).Value = v.Id;
                ws.Cell(i + 2, 2).Value = v.Fecha.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(i + 2, 3).Value = v.TipoCliente == "CONSUMIDOR_FINAL" ? "Consumidor final" : "Cliente registrado";
                ws.Cell(i + 2, 4).Value = v.TipoPago == "CUENTA_CORRIENTE" ? "Cuenta corriente" : "Contado";
                ws.Cell(i + 2, 5).Value = (double)v.Total;
                ws.Cell(i + 2, 5).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(i + 2, 6).Value = v.Estado switch
                {
                    "PENDIENTE_AUTORIZACION" => "Pendiente",
                    "RECHAZADA" => "Rechazada",
                    "ANULADA" => "Anulada",
                    _ => "Confirmada"
                };
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"historial_ventas_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // Exporta la lista de clientes con saldo deudor, su límite de crédito y el porcentaje de uso a Excel.
        public IActionResult ExportarDeudoresExcel()
        {
            var resumen = _finanzasRepo.ObtenerResumen();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Clientes Deudores");
            ws.Cell(1, 1).Value = "Cliente";
            ws.Cell(1, 2).Value = "Deuda total";
            ws.Cell(1, 3).Value = "Límite crédito";
            ws.Cell(1, 4).Value = "% Uso del límite";
            ws.Row(1).Style.Font.Bold = true;
            for (int i = 0; i < resumen.Deudores.Count; i++)
            {
                var d = resumen.Deudores[i];
                ws.Cell(i + 2, 1).Value = d.Nombre;
                ws.Cell(i + 2, 2).Value = (double)d.DeudaTotal;
                ws.Cell(i + 2, 3).Value = (double)d.LimiteCredito;
                ws.Cell(i + 2, 4).Value = d.LimiteCredito > 0 ? Math.Round((double)(d.DeudaTotal / d.LimiteCredito * 100), 1) : 0;
                ws.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            // Fila totales
            int tot = resumen.Deudores.Count + 2;
            ws.Cell(tot, 1).Value = "TOTAL";
            ws.Cell(tot, 2).Value = (double)resumen.DeudaTotal;
            ws.Cell(tot, 2).Style.Font.Bold = true;
            ws.Cell(tot, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"deudores_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // Exporta los productos con stock por debajo del mínimo configurado a un archivo Excel.
        public IActionResult ExportarStockCriticoExcel()
        {
            var productos = _stockRepo.GetProductosStockCritico(null, 1, int.MaxValue, out _).ToList();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Stock Crítico");
            ws.Cell(1, 1).Value = "SKU";
            ws.Cell(1, 2).Value = "Nombre";
            ws.Cell(1, 3).Value = "Stock actual";
            ws.Cell(1, 4).Value = "Stock mínimo";
            ws.Cell(1, 5).Value = "Unidad";
            ws.Cell(1, 6).Value = "Ubicación";
            ws.Cell(1, 7).Value = "Precio venta";
            ws.Row(1).Style.Font.Bold = true;
            for (int i = 0; i < productos.Count; i++)
            {
                var p = productos[i];
                ws.Cell(i + 2, 1).Value = p.Sku;
                ws.Cell(i + 2, 2).Value = p.Nombre;
                ws.Cell(i + 2, 3).Value = p.StockActual;
                ws.Cell(i + 2, 4).Value = p.StockMinimo;
                ws.Cell(i + 2, 5).Value = p.UnidadMedida;
                ws.Cell(i + 2, 6).Value = p.UbicacionCodigo ?? "-";
                ws.Cell(i + 2, 7).Value = (double)p.PrecioVentaActual;
                ws.Cell(i + 2, 7).Style.NumberFormat.Format = "#,##0.00";
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"stock_critico_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ── Print / PDF ────────────────────────────────────────────────────────────

        // Vista imprimible del resumen financiero, sin layout ni navegación, lista para Ctrl+P o exportar a PDF.
        public IActionResult ImprimirFinanzas()
        {
            var resumen = _finanzasRepo.ObtenerResumen();
            ViewData["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            return View(resumen);
        }

        // Vista imprimible del historial de ventas filtrado por fechas.
        public IActionResult ImprimirHistorial(string? desde, string? hasta)
        {
            var fechaDesde = ParseDate(desde)?.DateTime;
            var fechaHasta = ParseDate(hasta)?.DateTime;
            var ventas = _ventaRepo.GetParaExportar(fechaDesde, fechaHasta).ToList();
            ViewBag.Desde = desde;
            ViewBag.Hasta = hasta;
            ViewData["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            return View(ventas);
        }

        // Vista imprimible del reporte de clientes deudores.
        public IActionResult ImprimirDeudores()
        {
            var resumen = _finanzasRepo.ObtenerResumen();
            ViewData["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            return View(resumen);
        }

        // Vista imprimible del reporte de productos en stock crítico.
        public IActionResult ImprimirStockCritico()
        {
            var productos = _stockRepo.GetProductosStockCritico(null, 1, int.MaxValue, out _).ToList();
            ViewData["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            return View(productos);
        }

        // ── helpers ────────────────────────────────────────────────────────────────

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
