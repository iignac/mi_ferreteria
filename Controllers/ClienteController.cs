using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using mi_ferreteria.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace mi_ferreteria.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class ClienteController : BaseController
    {
        private readonly IClienteRepository _repo;
        private readonly IVentaRepository _ventaRepo;
        private readonly ILogger<ClienteController> _logger;
        private static readonly Regex NombreSoloLetrasRegex = new Regex(ValidationConstants.NombreSoloLetrasPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NumeroDocumentoSoloDigitosRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex MultipleSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);

        public ClienteController(IClienteRepository repo, IVentaRepository ventaRepo, IAuditoriaRepository auditoriaRepo, ILogger<ClienteController> logger)
            : base(auditoriaRepo)
        {
            _repo = repo;
            _ventaRepo = ventaRepo;
            _logger = logger;
        }

        // Lista los clientes paginados con soporte de búsqueda por texto y ordenamiento por columnas.
        public IActionResult Index(string? q = null, int page = 1, string? sort = null)
        {
            var normalizedSort = NormalizeClienteSort(sort);
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                int total = _repo.Count(q);
                int totalPages = (int)System.Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                var clientes = _repo.GetPage(q, page, pageSize, normalizedSort).ToList();

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;
                ViewBag.Query = q;
                ViewBag.Sort = normalizedSort;
                return View(clientes);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar clientes");
                ViewBag.Page = 1;
                ViewBag.PageSize = 10;
                ViewBag.TotalCount = 0;
                ViewBag.TotalPages = 1;
                ViewBag.Query = q;
                ViewBag.Sort = normalizedSort;
                return View(Enumerable.Empty<Cliente>());
            }
        }

        // Muestra el formulario de alta de cliente con valores por defecto. Soporta carga por modal AJAX.
        public IActionResult Create()
        {
            var vm = new ClienteCreateViewModel
            {
                Activo = true,
                TipoCliente = "CONSUMIDOR_FINAL",
                LimiteCredito = 0,
                SaldoInicialCuentaCorriente = 0
            };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(vm);
            return View(vm);
        }

        // Valida y persiste el nuevo cliente. Si tiene CC habilitada con saldo inicial, registra el ajuste de apertura.
        [HttpPost]
        public IActionResult Create(ClienteCreateViewModel model)
        {
            try
            {
                if (model == null)
                {
                    model = new ClienteCreateViewModel();
                    ModelState.AddModelError(string.Empty, "No se recibieron los datos del cliente.");
                }

                NormalizarYValidarCliente(model);

                if (!ModelState.IsValid)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }

                var cliente = MapearCliente(model);

                _repo.Add(cliente);

                RegistrarAuditoria(nameof(Create), $"Alta de cliente #{cliente.Id}: {ResumenCliente(cliente)}");

                if (EsCuentaCorriente(model) && model.SaldoInicialCuentaCorriente != 0)
                {
                    try
                    {
                        _repo.RegistrarSaldoInicial(cliente.Id, model.SaldoInicialCuentaCorriente);
                    }
                    catch (System.Exception exAdj)
                    {
                        _logger.LogError(exAdj, "Error al registrar saldo inicial de cuenta corriente para cliente {ClienteId}", cliente.Id);
                    }
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear cliente");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear el cliente.");
                if (model == null)
                {
                    model = new ClienteCreateViewModel();
                }
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                return View(model);
            }
        }
        [HttpGet]
        // Muestra el comprobante imprimible de un movimiento de cuenta corriente (pago, nota de crédito/débito, etc.).
        public IActionResult MovimientoComprobante(long clienteId, long movimientoId)
        {
            var cliente = _repo.GetById(clienteId);
            if (cliente == null) return NotFound();
            var movimiento = _repo.GetMovimiento(movimientoId);
            if (movimiento == null || movimiento.ClienteId != clienteId)
                return NotFound();
            var vm = new ClienteMovimientoComprobanteViewModel
            {
                Cliente = cliente,
                Movimiento = movimiento
            };
            return View("MovimientoComprobante", vm);
        }



        // Muestra el formulario de edición con los datos actuales del cliente. Soporta carga por modal AJAX.
        public IActionResult Edit(long id)
        {
            var cliente = _repo.GetById(id);
            if (cliente == null) return NotFound();
            var vm = ConstruirClienteViewModel(cliente);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(vm);
            return View(vm);
        }

        // Muestra el detalle del cliente. Si tiene CC habilitada, calcula y expone el saldo actual y disponible.
        public IActionResult Details(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            decimal? saldoActual = null;
            decimal? saldoDisponible = null;
            if (c.CuentaCorrienteHabilitada)
            {
                saldoActual = _repo.GetSaldoCuentaCorriente(id);
                saldoDisponible = c.LimiteCredito + saldoActual.GetValueOrDefault();
            }
            ViewBag.SaldoActual = saldoActual;
            ViewBag.SaldoDisponible = saldoDisponible;
            return View(c);
        }

        // Muestra las facturas asociadas a un cliente, con bÃºsqueda por comprobante y paginaciÃ³n.
        public IActionResult Facturas(long id, string? q = null, int page = 1)
        {
            var cliente = _repo.GetById(id);
            if (cliente == null) return NotFound();

            const int pageSize = 10;
            if (page < 1) page = 1;

            var totalFacturas = _ventaRepo.CountFacturasPorCliente(id, q);
            var totalPages = totalFacturas == 0 ? 1 : (int)Math.Ceiling(totalFacturas / (double)pageSize);
            if (page > totalPages) page = totalPages;

            var facturas = totalFacturas > 0
                ? _ventaRepo.GetFacturasPorCliente(id, q, page, pageSize).ToList()
                : new List<ClienteFacturaItemViewModel>();

            var vm = new ClienteFacturasViewModel
            {
                Cliente = cliente,
                Facturas = facturas,
                Query = q ?? string.Empty,
                Page = page,
                PageSize = pageSize,
                TotalFacturas = totalFacturas,
                TotalPages = totalPages
            };

            return View(vm);
        }

        // Muestra el estado completo de la cuenta corriente: movimientos, facturas pendientes, vencidas y saldo actual.
        public IActionResult CuentaCorriente(long id, int page = 1)
        {
            var cliente = _repo.GetById(id);
            if (cliente == null) return NotFound();

            const int pageSize = 10;
            if (page < 1) page = 1;

            var facturasPendientes = _repo.GetFacturasPendientes(id).ToList();
            var ahora = DateTimeOffset.UtcNow;
            var facturasVencidas = facturasPendientes.Where(f => f.FechaVencimiento < ahora).ToList();

            var totalMovimientos = _repo.CountMovimientosCuentaCorriente(id);
            var totalPages = totalMovimientos == 0 ? 1 : (int)Math.Ceiling(totalMovimientos / (double)pageSize);
            if (page > totalPages) page = totalPages;

            var movimientos = totalMovimientos > 0
                ? _repo.GetMovimientosCuentaCorriente(id, page, pageSize).ToList()
                : new List<ClienteCuentaCorrienteMovimiento>();

            var saldoActual = cliente.CuentaCorrienteHabilitada ? _repo.GetSaldoCuentaCorriente(id) : 0m;
            var saldoDisponible = cliente.CuentaCorrienteHabilitada ? cliente.LimiteCredito + saldoActual : cliente.LimiteCredito;

            var vm = new ClienteCuentaCorrienteViewModel
            {
                Cliente = cliente,
                Movimientos = movimientos,
                FacturasVencidas = facturasVencidas,
                FacturasPendientes = facturasPendientes,
                SaldoActual = saldoActual,
                SaldoDisponible = saldoDisponible,
                Page = page,
                PageSize = pageSize,
                TotalMovimientos = totalMovimientos,
                TotalPages = totalPages
            };
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        // Genera una nota de débito sobre una factura vencida de CC. Valida que la factura esté vencida y que el monto no supere el saldo pendiente.
        public IActionResult GenerarNotaDebito(long clienteId, long movimientoDeudaId, decimal monto, string? descripcion)
        {
            try
            {
                var cliente = _repo.GetById(clienteId);
                if (cliente == null) return NotFound();
                if (!cliente.CuentaCorrienteHabilitada)
                {
                    TempData["CuentaCorrienteError"] = "La cuenta corriente no estã‚± habilitada para este cliente.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var movimiento = _repo.GetMovimiento(movimientoDeudaId);
                if (movimiento == null || movimiento.ClienteId != clienteId || movimiento.Tipo != "DEUDA")
                {
                    TempData["CuentaCorrienteError"] = "El movimiento de deuda seleccionado no es vã‚±lido.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var facturasPendientes = _repo.GetFacturasPendientes(clienteId).ToList();
                var facturaObjetivo = facturasPendientes.FirstOrDefault(f => f.MovimientoDeudaId == movimientoDeudaId);
                if (facturaObjetivo == null || facturaObjetivo.FechaVencimiento >= DateTimeOffset.UtcNow)
                {
                    TempData["CuentaCorrienteError"] = "La factura seleccionada no estã‚± vencida o ya fue cancelada.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                if (monto <= 0 || monto > facturaObjetivo.SaldoPendiente)
                {
                    TempData["CuentaCorrienteError"] = "El monto debe ser mayor a cero y no puede superar el saldo pendiente.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                if (!TryGetAuditoriaUsuario(out var userId, out _))
                {
                    TempData["CuentaCorrienteError"] = "No se pudo identificar al usuario actual.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var descripcionFinal = string.IsNullOrWhiteSpace(descripcion)
                    ? $"Nota de débito por factura vencida {(facturaObjetivo.Comprobante ?? $"Venta {facturaObjetivo.VentaId}")}"
                    : descripcion.Trim();

                var movimientoId = _repo.RegistrarNotaDebito(clienteId, monto, userId, descripcionFinal, movimiento.VentaId, movimiento.Id);
                RegistrarAuditoria(nameof(GenerarNotaDebito), $"Nota de debito por ${monto:N2} para cliente {cliente.Nombre} (ID {cliente.Id}).");
                var comprobanteUrl = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId });
                TempData["CuentaCorrienteOk"] = $"La nota de debito se genero con exito. <a href=\"{comprobanteUrl}\" target=\"_blank\">Imprimir comprobante</a>.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al registrar nota de dÃ©bito para cliente {ClienteId}", clienteId);
                TempData["CuentaCorrienteError"] = "OcurriÃ³ un error al registrar la nota de dÐ˜bito.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Genera una nota de crédito o débito manual en CC. La nota de crédito aplica primero a deuda existente y el resto queda como saldo a favor.
        public IActionResult GenerarNotaCuentaCorriente(long clienteId, string tipoNota, decimal monto, string? descripcion)
        {
            try
            {
                var cliente = _repo.GetById(clienteId);
                if (cliente == null) return NotFound();
                if (!cliente.CuentaCorrienteHabilitada)
                {
                    TempData["NotaError"] = "La cuenta corriente no esta habilitada para este cliente.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }

                var tipo = (tipoNota ?? string.Empty).Trim().ToUpperInvariant();
                if (tipo != "CREDITO" && tipo != "DEBITO")
                {
                    TempData["NotaError"] = "Debe seleccionar el tipo de nota.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }

                if (monto <= 0)
                {
                    TempData["NotaError"] = "El monto de la nota debe ser mayor a cero.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }

                var saldoActual = _repo.GetSaldoCuentaCorriente(clienteId);
                var saldoDisponible = cliente.LimiteCredito + saldoActual;

                if (!TryGetAuditoriaUsuario(out var userId, out var usuarioNombre))
                {
                    TempData["NotaError"] = "No se pudo identificar al usuario actual.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }

                if (tipo == "CREDITO")
                {
                    var deudaActual = saldoActual < 0 ? Math.Abs(saldoActual) : 0m;

                    var descripcionFinal = string.IsNullOrWhiteSpace(descripcion)
                        ? "Nota de credito en cuenta corriente"
                        : descripcion.Trim();

                    long movimientoIdDeuda = 0;
                    long movimientoIdSaldo = 0;
                    decimal aplicadoDeuda = 0m;

                    if (deudaActual > 0)
                    {
                        aplicadoDeuda = Math.Min(monto, deudaActual);
                        var descDeuda = monto > deudaActual
                            ? $"{descripcionFinal}. Aplicada a deuda por ${aplicadoDeuda:N2}."
                            : descripcionFinal;
                        movimientoIdDeuda = _repo.RegistrarNotaCredito(clienteId, aplicadoDeuda, userId, descDeuda, null, null);
                    }

                    var saldoFavor = monto - aplicadoDeuda;
                    if (saldoFavor > 0)
                    {
                        var descSaldo = deudaActual > 0
                            ? $"{descripcionFinal}. Saldo a favor generado: ${saldoFavor:N2}."
                            : $"{descripcionFinal}. Saldo a favor.";
                        movimientoIdSaldo = _repo.RegistrarNotaCredito(clienteId, saldoFavor, userId, descSaldo, null, null);
                    }

                    RegistrarAuditoria(nameof(GenerarNotaCuentaCorriente),
                        $"Nota de credito por ${monto:N2} para cliente {cliente.Nombre} (ID {cliente.Id}){(aplicadoDeuda > 0 ? $", deuda saldada ${aplicadoDeuda:N2}" : string.Empty)}{(saldoFavor > 0 ? $", saldo a favor ${saldoFavor:N2}" : string.Empty)}.");

                    var links = new List<string>();
                    if (movimientoIdDeuda > 0)
                    {
                        var urlDeuda = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId = movimientoIdDeuda });
                        links.Add($"<a href=\"{urlDeuda}\" target=\"_blank\">Comprobante deuda</a>");
                    }
                    if (movimientoIdSaldo > 0)
                    {
                        var urlSaldo = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId = movimientoIdSaldo });
                        links.Add($"<a href=\"{urlSaldo}\" target=\"_blank\">Comprobante saldo a favor</a>");
                    }
                    if (links.Count == 0)
                    {
                        TempData["NotaError"] = "No se pudo registrar la nota de credito.";
                        return RedirectToAction(nameof(Details), new { id = clienteId });
                    }

                    TempData["NotaOk"] = $"La nota de credito se genero con exito. {string.Join(" - ", links)}.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }

                if (saldoDisponible <= 0)
                {
                    TempData["NotaError"] = "El cliente no tiene saldo disponible para generar una nota de debito.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }
                if (monto > saldoDisponible)
                {
                    TempData["NotaError"] = "El monto supera el saldo disponible del cliente.";
                    return RedirectToAction(nameof(Details), new { id = clienteId });
                }

                var descripcionDebito = string.IsNullOrWhiteSpace(descripcion)
                    ? "Nota de debito en cuenta corriente"
                    : descripcion.Trim();
                var movimientoDebitoId = _repo.RegistrarNotaDebito(clienteId, monto, userId, descripcionDebito, null, null);
                RegistrarAuditoria(nameof(GenerarNotaCuentaCorriente),
                    $"Nota de debito por ${monto:N2} para cliente {cliente.Nombre} (ID {cliente.Id}).");
                var comprobanteDebitoUrl = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId = movimientoDebitoId });
                TempData["NotaOk"] = $"La nota de debito se genero con exito. <a href=\"{comprobanteDebitoUrl}\" target=\"_blank\">Imprimir comprobante</a>.";
                return RedirectToAction(nameof(Details), new { id = clienteId });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al registrar nota en cuenta corriente para cliente {ClienteId}", clienteId);
                TempData["NotaError"] = "Ocurrio un error al registrar la nota.";
                return RedirectToAction(nameof(Details), new { id = clienteId });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Registra un pago en la CC del cliente. Puede aplicarse a una factura específica o al saldo general.
        public IActionResult RegistrarPagoCuentaCorriente(long clienteId, decimal monto, string? descripcion, long? movimientoDeudaId = null, int returnPage = 1)
        {
            var targetPage = returnPage < 1 ? 1 : returnPage;
            try
            {
                var cliente = _repo.GetById(clienteId);
                if (cliente == null) return NotFound();
                if (!cliente.CuentaCorrienteHabilitada)
                {
                    TempData["CuentaCorrienteError"] = "La cuenta corriente no estã‚± habilitada para este cliente.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
                }

                if (monto <= 0)
                {
                    TempData["CuentaCorrienteError"] = "El monto del pago debe ser mayor a cero.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
                }

                List<ClienteCuentaCorrienteFacturaPendiente>? facturasPendientes = null;
                long? ventaId = null;
                long? movRelacionadoId = null;
                if (movimientoDeudaId.HasValue)
                {
                    facturasPendientes = _repo.GetFacturasPendientes(clienteId).ToList();
                    var factura = facturasPendientes
                        .FirstOrDefault(f => f.MovimientoDeudaId == movimientoDeudaId.Value);
                    if (factura == null)
                    {
                        TempData["CuentaCorrienteError"] = "La factura seleccionada no tiene saldo pendiente.";
                        return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
                    }
                    if (monto > factura.SaldoPendiente)
                    {
                        TempData["CuentaCorrienteError"] = "El monto supera el saldo pendiente de la factura seleccionada.";
                        return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
                    }
                    ventaId = factura.VentaId;
                    movRelacionadoId = factura.MovimientoDeudaId;
                }

                if (!TryGetAuditoriaUsuario(out var userId, out _))
                {
                    TempData["CuentaCorrienteError"] = "No se pudo identificar al usuario actual.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
                }

                var descripcionFinal = string.IsNullOrWhiteSpace(descripcion)
                    ? "Pago registrado en cuenta corriente"
                    : descripcion.Trim();

                var pagoMovimientoId = _repo.RegistrarPagoCuentaCorriente(clienteId, monto, userId, descripcionFinal, ventaId, movRelacionadoId);
                RegistrarAuditoria(nameof(RegistrarPagoCuentaCorriente),
                    $"Pago por ${monto:N2} en cuenta corriente de cliente {cliente.Nombre} (ID {cliente.Id}){(ventaId.HasValue ? $" aplicado a venta #{ventaId}" : string.Empty)}.");
                var pagoUrl = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId = pagoMovimientoId });
                TempData["CuentaCorrienteOk"] = $"El pago se registro correctamente. <a href=\"{pagoUrl}\" target=\"_blank\">Imprimir recibo</a>.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al registrar pago de cuenta corriente para cliente {ClienteId}", clienteId);
                TempData["CuentaCorrienteError"] = "Ocurriç±€ un error al registrar el pago.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId, page = targetPage });
            }
        }

        // Valida y actualiza los datos del cliente. Registra en auditoría los cambios de nombre, tipo, estado y CC.
        [HttpPost]
        public IActionResult Edit(long id, ClienteCreateViewModel model)
        {
            try
            {
                if (model == null)
                {
                    model = new ClienteCreateViewModel { Id = id };
                    ModelState.AddModelError(string.Empty, "No se recibieron los datos del cliente.");
                }

                model.Id ??= id;
                if (!model.Id.HasValue || model.Id.Value <= 0)
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.Id), "El identificador del cliente es inválido.");
                }

                NormalizarYValidarCliente(model);

                if (!ModelState.IsValid)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                    return View(model);
                }

                var anterior = _repo.GetById(model.Id.Value);
                if (anterior == null) return NotFound();

                var cliente = MapearCliente(model);
                cliente.Id = anterior.Id;

                _repo.Update(cliente);
                var antesCc = anterior.CuentaCorrienteHabilitada ? $"SI (limite {anterior.LimiteCredito:N2})" : "NO";
                var ahoraCc = cliente.CuentaCorrienteHabilitada ? $"SI (limite {cliente.LimiteCredito:N2})" : "NO";
                RegistrarAuditoria(nameof(Edit),
                    $"Actualizacion cliente #{cliente.Id}: nombre '{anterior.Nombre}' -> '{cliente.Nombre}', tipo '{anterior.TipoCliente}' -> '{cliente.TipoCliente}', activo {anterior.Activo} -> {cliente.Activo}, CC {antesCc} -> {ahoraCc}.");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar cliente {ClienteId}", id);
                ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar el cliente.");
                if (model == null)
                {
                    model = new ClienteCreateViewModel { Id = id };
                }
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(model);
                return View(model);
            }
        }
        // Realiza la baja lógica del cliente marcándolo como inactivo (no se elimina de la BD).
        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(long id) => CambiarEstadoCliente(id, false);

        // Reactiva un cliente previamente dado de baja y registra la acción en auditoría.
        [HttpPost]
        public IActionResult Activate(long id) => CambiarEstadoCliente(id, true);

        private IActionResult CambiarEstadoCliente(long id, bool activo)
        {
            try
            {
                var c = _repo.GetById(id);
                if (c == null) return NotFound();
                c.Activo = activo;
                _repo.Update(c);
                var accionNombre = activo ? nameof(Activate) : "Delete";
                var msg = activo
                    ? $"Cliente #{c.Id}: reactivado ({c.Nombre} {c.Apellido})."
                    : $"Cliente #{c.Id}: dado de baja ({c.Nombre} {c.Apellido}).";
                RegistrarAuditoria(accionNombre, msg);
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del cliente {ClienteId} a activo={Activo}", id, activo);
                return Problem(activo ? "Ocurrió un error al activar el cliente." : "Ocurrió un error al dar de baja el cliente.");
            }
        }

        private static string NormalizeClienteSort(string? sort)
        {
            return sort switch
            {
                "nombre_desc" => "nombre_desc",
                "tipocliente_asc" => "tipocliente_asc",
                "tipocliente_desc" => "tipocliente_desc",
                "limite_asc" => "limite_asc",
                "limite_desc" => "limite_desc",
                "estado_asc" => "estado_asc",
                "estado_desc" => "estado_desc",
                _ => "nombre_asc"
            };
        }

        private static string ResumenCliente(Cliente cliente)
        {
            var nombre = cliente.Nombre?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(cliente.Apellido))
            {
                nombre = string.IsNullOrWhiteSpace(nombre) ? cliente.Apellido.Trim() : $"{nombre} {cliente.Apellido.Trim()}";
            }
            var email = string.IsNullOrWhiteSpace(cliente.Email) ? "sin email" : cliente.Email.Trim();
            var cuenta = cliente.CuentaCorrienteHabilitada
                ? $"CC habilitada (limite {cliente.LimiteCredito:N2})"
                : "CC deshabilitada";
            return $"{nombre} - Tipo {cliente.TipoCliente}, {cuenta}, Email {email}, Activo={cliente.Activo}";
        }
        private void NormalizarYValidarCliente(ClienteCreateViewModel model)
        {
            model.Nombre = InputSanitizer.NormalizeName(model.Nombre) ?? string.Empty;
            var normalizedApellido = InputSanitizer.NormalizeName(model.Apellido);
            model.Apellido = string.IsNullOrWhiteSpace(normalizedApellido) ? null : normalizedApellido;
            var tipoDocNorm = string.IsNullOrWhiteSpace(model.TipoDocumento) ? null : model.TipoDocumento.Trim().ToUpperInvariant();
            model.TipoDocumento = tipoDocNorm;
            model.NumeroDocumento = string.IsNullOrWhiteSpace(model.NumeroDocumento) ? null : model.NumeroDocumento.Trim();
            if (!string.IsNullOrWhiteSpace(model.NumeroDocumento) && !NumeroDocumentoSoloDigitosRegex.IsMatch(model.NumeroDocumento))
            {
                ModelState.AddModelError(nameof(ClienteCreateViewModel.NumeroDocumento), "El número de DNI/CUIT debe tener solo dígitos (sin puntos ni guiones).");
            }
            if (!string.IsNullOrWhiteSpace(model.DireccionCalleNumero))
            {
                var calleNormalizada = model.DireccionCalleNumero.Trim();
                calleNormalizada = MultipleSpacesRegex.Replace(calleNormalizada, " ");
                model.DireccionCalleNumero = calleNormalizada;
            }
            else
            {
                model.DireccionCalleNumero = null;
            }
            if (!string.IsNullOrWhiteSpace(model.DireccionPisoDpto))
            {
                var pisoNormalizado = model.DireccionPisoDpto.Trim();
                pisoNormalizado = MultipleSpacesRegex.Replace(pisoNormalizado, " ");
                model.DireccionPisoDpto = pisoNormalizado;
            }
            else
            {
                model.DireccionPisoDpto = null;
            }
            model.DireccionLocalidad = string.IsNullOrWhiteSpace(model.DireccionLocalidad) ? null : model.DireccionLocalidad.Trim();
            model.Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim();
            if (!string.IsNullOrWhiteSpace(model.Telefono))
            {
                model.Telefono = MultipleSpacesRegex.Replace(model.Telefono, " ");
            }
            model.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim().ToLowerInvariant();
            model.TipoCliente = string.IsNullOrWhiteSpace(model.TipoCliente) ? "CONSUMIDOR_FINAL" : model.TipoCliente.Trim().ToUpperInvariant();
            var esCuentaCorriente = string.Equals(model.TipoCliente, "CUENTA_CORRIENTE", System.StringComparison.OrdinalIgnoreCase);

            var requiereDireccion = string.Equals(tipoDocNorm, "CUIT", System.StringComparison.OrdinalIgnoreCase) || esCuentaCorriente;
            if (requiereDireccion)
            {
                if (string.IsNullOrWhiteSpace(model.DireccionCalleNumero))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.DireccionCalleNumero), "La calle y número son obligatorios para clientes con CUIT o cuenta corriente.");
                }
                if (string.IsNullOrWhiteSpace(model.DireccionLocalidad))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.DireccionLocalidad), "La localidad es obligatoria para clientes con CUIT o cuenta corriente.");
                }
            }

            if (tipoDocNorm == "DNI")
            {
                if (!NombreSoloLetrasRegex.IsMatch(model.Nombre ?? string.Empty))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.Nombre), "El nombre solo puede contener letras, espacios, apóstrofes o guiones.");
                }

                if (string.IsNullOrWhiteSpace(model.Apellido))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.Apellido), "El apellido es obligatorio cuando el tipo de documento es DNI.");
                }
                else if (!NombreSoloLetrasRegex.IsMatch(model.Apellido))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.Apellido), "El apellido solo puede contener letras, espacios, apóstrofes o guiones.");
                }

                if (string.IsNullOrWhiteSpace(model.NumeroDocumento))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.NumeroDocumento), "El DNI es obligatorio.");
                }
                else if (model.NumeroDocumento.Length < 7 || model.NumeroDocumento.Length > 8)
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.NumeroDocumento), "El DNI debe tener 7 u 8 dígitos.");
                }
            }
            else if (tipoDocNorm == "CUIT")
            {
                model.Apellido = null;
                if (string.IsNullOrWhiteSpace(model.NumeroDocumento))
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.NumeroDocumento), "El CUIT es obligatorio.");
                }
                else if (model.NumeroDocumento.Length != 11)
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.NumeroDocumento), "El CUIT debe tener 11 dígitos.");
                }
            }
            else
            {
                model.Apellido = null;
            }

            if (esCuentaCorriente)
            {
                if (model.LimiteCredito < 0)
                {
                    ModelState.AddModelError(nameof(ClienteCreateViewModel.LimiteCredito), "El límite de crédito no puede ser negativo.");
                }
            }
            else
            {
                model.LimiteCredito = 0;
            }
        }

        private Cliente MapearCliente(ClienteCreateViewModel model)
        {
            var direccion = BuildDireccion(model.DireccionCalleNumero, model.DireccionPisoDpto, model.DireccionLocalidad);
            var esCuentaCorriente = EsCuentaCorriente(model);
            return new Cliente
            {
                Nombre = model.Nombre,
                Apellido = model.Apellido,
                TipoDocumento = model.TipoDocumento,
                NumeroDocumento = model.NumeroDocumento,
                Direccion = direccion,
                Telefono = model.Telefono,
                Email = model.Email,
                TipoCliente = model.TipoCliente,
                CuentaCorrienteHabilitada = esCuentaCorriente,
                LimiteCredito = esCuentaCorriente ? model.LimiteCredito : 0,
                Activo = model.Activo
            };
        }

        private ClienteCreateViewModel ConstruirClienteViewModel(Cliente cliente)
        {
            var (calleNumero, pisoDpto, localidad) = DescomponerDireccion(cliente.Direccion);
            return new ClienteCreateViewModel
            {
                Id = cliente.Id,
                Nombre = cliente.Nombre ?? string.Empty,
                Apellido = cliente.Apellido,
                TipoDocumento = cliente.TipoDocumento,
                NumeroDocumento = cliente.NumeroDocumento,
                DireccionCalleNumero = calleNumero ?? cliente.Direccion,
                DireccionPisoDpto = pisoDpto,
                DireccionLocalidad = localidad,
                Telefono = cliente.Telefono,
                Email = cliente.Email,
                TipoCliente = cliente.TipoCliente ?? (cliente.CuentaCorrienteHabilitada ? "CUENTA_CORRIENTE" : "CONSUMIDOR_FINAL"),
                LimiteCredito = cliente.CuentaCorrienteHabilitada ? cliente.LimiteCredito : 0,
                Activo = cliente.Activo
            };
        }

        private static bool EsCuentaCorriente(ClienteCreateViewModel? model)
        {
            if (model == null) return false;
            return string.Equals(model.TipoCliente?.Trim(), "CUENTA_CORRIENTE", System.StringComparison.OrdinalIgnoreCase);
        }

        private static (string? CalleNumero, string? PisoDpto, string? Localidad) DescomponerDireccion(string? direccion)
        {
            if (string.IsNullOrWhiteSpace(direccion))
            {
                return (null, null, null);
            }

            var partes = direccion
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (partes.Count == 0)
            {
                return (direccion.Trim(), null, null);
            }

            var calleNumero = partes[0];
            string? piso = null;
            string? localidad = null;

            if (partes.Count >= 3)
            {
                localidad = partes[^1];
                piso = string.Join(", ", partes.Skip(1).Take(partes.Count - 2));
            }
            else if (partes.Count == 2)
            {
                localidad = partes[1];
            }

            return (
                string.IsNullOrWhiteSpace(calleNumero) ? null : calleNumero,
                string.IsNullOrWhiteSpace(piso) ? null : piso,
                string.IsNullOrWhiteSpace(localidad) ? null : localidad);
        }

        private static string? BuildDireccion(string? calleNumero, string? pisoDpto, string? localidad)
        {
            calleNumero = calleNumero?.Trim();
            pisoDpto = pisoDpto?.Trim();
            localidad = localidad?.Trim();

            if (string.IsNullOrWhiteSpace(calleNumero) && string.IsNullOrWhiteSpace(pisoDpto) && string.IsNullOrWhiteSpace(localidad))
            {
                return null;
            }

            var partes = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(calleNumero))
            {
                partes.Add(calleNumero);
            }
            if (!string.IsNullOrWhiteSpace(pisoDpto))
            {
                partes.Add(pisoDpto);
            }
            if (!string.IsNullOrWhiteSpace(localidad))
            {
                partes.Add(localidad);
            }
            return string.Join(", ", partes);
        }
    }
}








