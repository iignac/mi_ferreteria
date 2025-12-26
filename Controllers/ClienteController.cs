using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;

namespace mi_ferreteria.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class ClienteController : Controller
    {
        private readonly IClienteRepository _repo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly ILogger<ClienteController> _logger;

        public ClienteController(IClienteRepository repo, IAuditoriaRepository auditoriaRepo, ILogger<ClienteController> logger)
        {
            _repo = repo;
            _auditoriaRepo = auditoriaRepo;
            _logger = logger;
        }

        public IActionResult Index(string? q = null, int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                int total = _repo.Count(q);
                int totalPages = (int)System.Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                var clientes = _repo.GetPage(q, page, pageSize).ToList();

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;
                ViewBag.Query = q;
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
                return View(Enumerable.Empty<Cliente>());
            }
        }

        public IActionResult Create()
        {
            var c = new Cliente
            {
                Activo = true,
                TipoCliente = "CONSUMIDOR_FINAL",
                CuentaCorrienteHabilitada = false,
                LimiteCredito = 0
            };
            return View(c);
        }

        [HttpPost]
        public IActionResult Create([Required] string Nombre, string? Apellido, string? TipoDocumento, string? NumeroDocumento,
                                    string? DireccionCalle, string? DireccionNumero, string? DireccionLocalidad,
                                    string? Telefono, string? Email,
                                    string TipoCliente, bool CuentaCorrienteHabilitada,
                                    decimal LimiteCredito, decimal SaldoInicialCuentaCorriente, bool Activo = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Nombre))
                {
                    ModelState.AddModelError("Nombre", "El nombre o razón social es obligatorio.");
                }

                var tipoDocNorm = TipoDocumento?.Trim().ToUpperInvariant();
                if (tipoDocNorm == "DNI")
                {
                    if (string.IsNullOrWhiteSpace(Apellido))
                    {
                        ModelState.AddModelError("Apellido", "El apellido es obligatorio cuando el tipo de documento es DNI.");
                    }
                    if (string.IsNullOrWhiteSpace(NumeroDocumento))
                    {
                        ModelState.AddModelError("NumeroDocumento", "El DNI es obligatorio.");
                    }
                    else if (NumeroDocumento.Trim().Length > 8)
                    {
                        ModelState.AddModelError("NumeroDocumento", "El DNI no puede tener más de 8 caracteres.");
                    }
                }
                else if (tipoDocNorm == "CUIT")
                {
                    if (string.IsNullOrWhiteSpace(NumeroDocumento))
                    {
                        ModelState.AddModelError("NumeroDocumento", "El CUIT es obligatorio.");
                    }
                    else if (NumeroDocumento.Trim().Length > 11)
                    {
                        ModelState.AddModelError("NumeroDocumento", "El CUIT no puede tener más de 11 caracteres.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(Email))
                {
                    var emailAttr = new EmailAddressAttribute();
                    if (!emailAttr.IsValid(Email))
                    {
                        ModelState.AddModelError("Email", "El email no tiene un formato válido.");
                    }
                }

                if (TipoCliente != "CONSUMIDOR_FINAL" && TipoCliente != "CUENTA_CORRIENTE")
                {
                    ModelState.AddModelError("TipoCliente", "Tipo de cliente inválido.");
                }

                if (CuentaCorrienteHabilitada && LimiteCredito < 0)
                {
                    ModelState.AddModelError("LimiteCredito", "El límite de crédito no puede ser negativo.");
                }

                var direccion = BuildDireccion(DireccionCalle, DireccionNumero, DireccionLocalidad);

                var cliente = new Cliente
                {
                    Nombre = Nombre,
                    Apellido = string.IsNullOrWhiteSpace(Apellido) ? null : Apellido,
                    TipoDocumento = string.IsNullOrWhiteSpace(TipoDocumento) ? null : TipoDocumento,
                    NumeroDocumento = string.IsNullOrWhiteSpace(NumeroDocumento) ? null : NumeroDocumento,
                    Direccion = direccion,
                    Telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono,
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
                    TipoCliente = TipoCliente,
                    CuentaCorrienteHabilitada = CuentaCorrienteHabilitada,
                    LimiteCredito = CuentaCorrienteHabilitada ? LimiteCredito : 0,
                    Activo = Activo
                };

                if (!ModelState.IsValid)
                {
                    return View(cliente);
                }

                _repo.Add(cliente);

                if (TryGetAuditoriaUsuario(out var userId, out var usuarioNombre))
                {
                    RegistrarAuditoria(userId, usuarioNombre, nameof(Create),
                        $"Alta de cliente #{cliente.Id}: {ResumenCliente(cliente)}");
                }

                // Saldo inicial de cuenta corriente como ajuste, si corresponde
                if (CuentaCorrienteHabilitada && SaldoInicialCuentaCorriente != 0)
                {
                    try
                    {
                        using var conn = new Npgsql.NpgsqlConnection(
                            (typeof(ClienteRepository)
                                .GetField("_connectionString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                                .GetValue(_repo) as string) ?? string.Empty);
                        conn.Open();
                        using var set = new Npgsql.NpgsqlCommand("SET search_path TO venta, public", conn);
                        set.ExecuteNonQuery();
                        using var cmdAdj = new Npgsql.NpgsqlCommand(@"
                            INSERT INTO cliente_cuenta_corriente_mov
                                (cliente_id, venta_id, tipo, monto, descripcion, usuario_id)
                            VALUES (@cid, NULL, 'AJUSTE', @monto, @desc, NULL)", conn);
                        cmdAdj.Parameters.AddWithValue("@cid", cliente.Id);
                        cmdAdj.Parameters.AddWithValue("@monto", SaldoInicialCuentaCorriente);
                        cmdAdj.Parameters.AddWithValue("@desc", (object)"Saldo inicial" ?? (object)System.DBNull.Value);
                        cmdAdj.ExecuteNonQuery();
                    }
                    catch (System.Exception exAdj)
                    {
                        _logger.LogError(exAdj, "Error al registrar saldo inicial de cuenta corriente para cliente {ClienteId}", cliente.Id);
                    }
                }

                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear cliente");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear el cliente.");
                var direccion = BuildDireccion(DireccionCalle, DireccionNumero, DireccionLocalidad);
                return View(new Cliente
                {
                    Nombre = Nombre ?? string.Empty,
                    Apellido = Apellido,
                    TipoDocumento = TipoDocumento,
                    NumeroDocumento = NumeroDocumento,
                    Direccion = direccion,
                    Telefono = Telefono,
                    Email = Email,
                    TipoCliente = TipoCliente,
                    CuentaCorrienteHabilitada = CuentaCorrienteHabilitada,
                    LimiteCredito = LimiteCredito,
                    Activo = Activo
                });
            }
        }

        [HttpGet]
        public IActionResult MovimientoComprobante(long clienteId, long movimientoId)
        {
            var cliente = _repo.GetById(clienteId);
            if (cliente == null) return NotFound();
            var movimiento = _repo.GetMovimiento(movimientoId);
            if (movimiento == null || movimiento.ClienteId != clienteId)
            {
                return NotFound();
            }

            var vm = new ClienteMovimientoComprobanteViewModel
            {
                Cliente = cliente,
                Movimiento = movimiento
            };
            return View("MovimientoComprobante", vm);
        }

        public IActionResult Edit(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            return View(c);
        }

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

        public IActionResult CuentaCorriente(long id)
        {
            var cliente = _repo.GetById(id);
            if (cliente == null) return NotFound();
            var movimientos = _repo.GetMovimientosCuentaCorriente(id).ToList();
            var facturasPendientes = _repo.GetFacturasPendientes(id).ToList();
            var ahora = DateTimeOffset.UtcNow;
            var facturasVencidas = facturasPendientes.Where(f => f.FechaVencimiento < ahora).ToList();
            var saldoActual = cliente.CuentaCorrienteHabilitada ? _repo.GetSaldoCuentaCorriente(id) : 0m;
            var saldoDisponible = cliente.CuentaCorrienteHabilitada ? cliente.LimiteCredito + saldoActual : cliente.LimiteCredito;
            var vm = new ClienteCuentaCorrienteViewModel
            {
                Cliente = cliente,
                Movimientos = movimientos,
                FacturasVencidas = facturasVencidas,
                FacturasPendientes = facturasPendientes,
                SaldoActual = saldoActual,
                SaldoDisponible = saldoDisponible
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerarNotaDebito(long clienteId, long movimientoDeudaId, decimal monto, string? descripcion)
        {
            try
            {
                var cliente = _repo.GetById(clienteId);
                if (cliente == null) return NotFound();
                if (!cliente.CuentaCorrienteHabilitada)
                {
                    TempData["CuentaCorrienteError"] = "La cuenta corriente no estケ habilitada para este cliente.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var movimiento = _repo.GetMovimiento(movimientoDeudaId);
                if (movimiento == null || movimiento.ClienteId != clienteId || movimiento.Tipo != "DEUDA")
                {
                    TempData["CuentaCorrienteError"] = "El movimiento de deuda seleccionado no es vケlido.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var facturasPendientes = _repo.GetFacturasPendientes(clienteId).ToList();
                var facturaObjetivo = facturasPendientes.FirstOrDefault(f => f.MovimientoDeudaId == movimientoDeudaId);
                if (facturaObjetivo == null || facturaObjetivo.FechaVencimiento >= DateTimeOffset.UtcNow)
                {
                    TempData["CuentaCorrienteError"] = "La factura seleccionada no estケ vencida o ya fue cancelada.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                if (monto <= 0 || monto > facturaObjetivo.SaldoPendiente)
                {
                    TempData["CuentaCorrienteError"] = "El monto debe ser mayor a cero y no puede superar el saldo pendiente.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
                {
                    TempData["CuentaCorrienteError"] = "No se pudo identificar al usuario actual.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var descripcionFinal = string.IsNullOrWhiteSpace(descripcion)
                    ? $"Nota de débito por factura vencida {(facturaObjetivo.Comprobante ?? $"Venta {facturaObjetivo.VentaId}")}"
                    : descripcion.Trim();

                var usuarioNombre = User?.Identity?.Name ?? $"Usuario {userId}";
                                var movimientoId = _repo.RegistrarNotaDebito(clienteId, monto, userId, descripcionFinal, movimiento.VentaId, movimiento.Id);

                RegistrarAuditoria(userId, usuarioNombre, nameof(GenerarNotaDebito),

                    $"Nota de debito por ${monto:N2} para cliente {cliente.Nombre} (ID {cliente.Id}).");

                var comprobanteUrl = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId });

                TempData["CuentaCorrienteOk"] = $"La nota de debito se genero con exito. <a href=\"{comprobanteUrl}\" target=\"_blank\">Imprimir comprobante</a>.";

                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al registrar nota de débito para cliente {ClienteId}", clienteId);
                TempData["CuentaCorrienteError"] = "Ocurrió un error al registrar la nota de dИbito.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

                    RegistrarAuditoria(userId, usuarioNombre, nameof(GenerarNotaCuentaCorriente),
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
                RegistrarAuditoria(userId, usuarioNombre, nameof(GenerarNotaCuentaCorriente),
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
        public IActionResult RegistrarPagoCuentaCorriente(long clienteId, decimal monto, string? descripcion, long? movimientoDeudaId = null)
        {
            try
            {
                var cliente = _repo.GetById(clienteId);
                if (cliente == null) return NotFound();
                if (!cliente.CuentaCorrienteHabilitada)
                {
                    TempData["CuentaCorrienteError"] = "La cuenta corriente no estケ habilitada para este cliente.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                if (monto <= 0)
                {
                    TempData["CuentaCorrienteError"] = "El monto del pago debe ser mayor a cero.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
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
                        return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                    }
                    if (monto > factura.SaldoPendiente)
                    {
                        TempData["CuentaCorrienteError"] = "El monto supera el saldo pendiente de la factura seleccionada.";
                        return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                    }
                    ventaId = factura.VentaId;
                    movRelacionadoId = factura.MovimientoDeudaId;
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
                {
                    TempData["CuentaCorrienteError"] = "No se pudo identificar al usuario actual.";
                    return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
                }

                var descripcionFinal = string.IsNullOrWhiteSpace(descripcion)
                    ? "Pago registrado en cuenta corriente"
                    : descripcion.Trim();

                var pagoMovimientoId = _repo.RegistrarPagoCuentaCorriente(clienteId, monto, userId, descripcionFinal, ventaId, movRelacionadoId);
                var usuarioNombre = User?.Identity?.Name ?? $"Usuario {userId}";
                RegistrarAuditoria(userId, usuarioNombre, nameof(RegistrarPagoCuentaCorriente),
                    $"Pago por ${monto:N2} en cuenta corriente de cliente {cliente.Nombre} (ID {cliente.Id}){(ventaId.HasValue ? $" aplicado a venta #{ventaId}" : string.Empty)}.");
                var pagoUrl = Url.Action(nameof(MovimientoComprobante), new { clienteId, movimientoId = pagoMovimientoId });
                TempData["CuentaCorrienteOk"] = $"El pago se registro correctamente. <a href=\"{pagoUrl}\" target=\"_blank\">Imprimir recibo</a>.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al registrar pago de cuenta corriente para cliente {ClienteId}", clienteId);
                TempData["CuentaCorrienteError"] = "Ocurri籀 un error al registrar el pago.";
                return RedirectToAction(nameof(CuentaCorriente), new { id = clienteId });
            }
        }

        [HttpPost]
        public IActionResult Edit(long Id, [Required] string Nombre, string? Apellido, string? TipoDocumento, string? NumeroDocumento,
                                  string? DireccionCalle, string? DireccionNumero, string? DireccionLocalidad,
                                  string? Telefono, string? Email,
                                  string TipoCliente, bool CuentaCorrienteHabilitada,
                                  decimal LimiteCredito, bool Activo = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Nombre))
                {
                    ModelState.AddModelError("Nombre", "El nombre o razón social es obligatorio.");
                }

                var tipoDocNorm = TipoDocumento?.Trim().ToUpperInvariant();
                if (tipoDocNorm == "DNI")
                {
                    if (string.IsNullOrWhiteSpace(Apellido))
                    {
                        ModelState.AddModelError("Apellido", "El apellido es obligatorio cuando el tipo de documento es DNI.");
                    }
                    if (string.IsNullOrWhiteSpace(NumeroDocumento))
                    {
                        ModelState.AddModelError("NumeroDocumento", "El DNI es obligatorio.");
                    }
                    else if (NumeroDocumento.Trim().Length > 8)
                    {
                        ModelState.AddModelError("NumeroDocumento", "El DNI no puede tener más de 8 caracteres.");
                    }
                }
                else if (tipoDocNorm == "CUIT")
                {
                    if (string.IsNullOrWhiteSpace(NumeroDocumento))
                    {
                        ModelState.AddModelError("NumeroDocumento", "El CUIT es obligatorio.");
                    }
                    else if (NumeroDocumento.Trim().Length > 11)
                    {
                        ModelState.AddModelError("NumeroDocumento", "El CUIT no puede tener más de 11 caracteres.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(Email))
                {
                    var emailAttr = new EmailAddressAttribute();
                    if (!emailAttr.IsValid(Email))
                    {
                        ModelState.AddModelError("Email", "El email no tiene un formato válido.");
                    }
                }

                if (TipoCliente != "CONSUMIDOR_FINAL" && TipoCliente != "CUENTA_CORRIENTE")
                {
                    ModelState.AddModelError("TipoCliente", "Tipo de cliente inválido.");
                }

                if (CuentaCorrienteHabilitada && LimiteCredito < 0)
                {
                    ModelState.AddModelError("LimiteCredito", "El límite de crédito no puede ser negativo.");
                }

                var direccion = BuildDireccion(DireccionCalle, DireccionNumero, DireccionLocalidad);

                var cliente = new Cliente
                {
                    Id = Id,
                    Nombre = Nombre,
                    Apellido = string.IsNullOrWhiteSpace(Apellido) ? null : Apellido,
                    TipoDocumento = string.IsNullOrWhiteSpace(TipoDocumento) ? null : TipoDocumento,
                    NumeroDocumento = string.IsNullOrWhiteSpace(NumeroDocumento) ? null : NumeroDocumento,
                    Direccion = direccion,
                    Telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono,
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
                    TipoCliente = TipoCliente,
                    CuentaCorrienteHabilitada = CuentaCorrienteHabilitada,
                    LimiteCredito = CuentaCorrienteHabilitada ? LimiteCredito : 0,
                    Activo = Activo
                };

                if (!ModelState.IsValid)
                {
                    return View(cliente);
                }

                var anterior = _repo.GetById(Id);
                if (anterior == null) return NotFound();

                _repo.Update(cliente);
                if (TryGetAuditoriaUsuario(out var userId, out var usuarioNombre))
                {
                    RegistrarAuditoria(userId, usuarioNombre, nameof(Edit),
                        $"Actualizacion cliente #{cliente.Id}: nombre '{anterior.Nombre}' -> '{cliente.Nombre}', tipo '{anterior.TipoCliente}' -> '{cliente.TipoCliente}', activo {anterior.Activo} -> {cliente.Activo}, CC {(anterior.CuentaCorrienteHabilitada ? $"SI (limite {anterior.LimiteCredito:N2})" : "NO")} -> {(cliente.CuentaCorrienteHabilitada ? $"SI (limite {cliente.LimiteCredito:N2})" : "NO")}.");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar cliente {ClienteId}", Id);
                ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar el cliente.");
                var direccion = BuildDireccion(DireccionCalle, DireccionNumero, DireccionLocalidad);
                return View(new Cliente
                {
                    Id = Id,
                    Nombre = Nombre ?? string.Empty,
                    Apellido = Apellido,
                    TipoDocumento = TipoDocumento,
                    NumeroDocumento = NumeroDocumento,
                    Direccion = direccion,
                    Telefono = Telefono,
                    Email = Email,
                    TipoCliente = TipoCliente,
                    CuentaCorrienteHabilitada = CuentaCorrienteHabilitada,
                    LimiteCredito = LimiteCredito,
                    Activo = Activo
                });
            }
        }

        public IActionResult Delete(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(long id)
        {
            try
            {
                var c = _repo.GetById(id);
                if (c == null) return NotFound();
                c.Activo = false;
                _repo.Update(c);
                if (TryGetAuditoriaUsuario(out var userId, out var usuarioNombre))
                {
                    RegistrarAuditoria(userId, usuarioNombre, "Delete", $"Cliente #{c.Id}: dado de baja ({c.Nombre} {c.Apellido}).");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al dar de baja cliente {ClienteId}", id);
                return Problem("Ocurrió un error al dar de baja el cliente.");
            }
        }

        [HttpPost]
        public IActionResult Activate(long id)
        {
            try
            {
                var c = _repo.GetById(id);
                if (c == null) return NotFound();
                c.Activo = true;
                _repo.Update(c);
                if (TryGetAuditoriaUsuario(out var userId, out var usuarioNombre))
                {
                    RegistrarAuditoria(userId, usuarioNombre, nameof(Activate), $"Cliente #{c.Id}: reactivado ({c.Nombre} {c.Apellido}).");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al activar cliente {ClienteId}", id);
                return Problem("Ocurrió un error al activar el cliente.");
            }
        }

        private bool TryGetAuditoriaUsuario(out int userId, out string usuarioNombre)
        {
            usuarioNombre = User?.Identity?.Name ?? "Usuario desconocido";
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out userId) || userId <= 0)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(usuarioNombre))
            {
                usuarioNombre = $"Usuario {userId}";
            }
            return true;
        }

        private void RegistrarAuditoria(int userId, string usuarioNombre, string accion, string detalle)
        {
            var finalAccion = BuildAccionNombre(accion);
            _auditoriaRepo.Registrar(userId, usuarioNombre, finalAccion, detalle);
            HttpContext.Items["AuditLogged"] = true;
        }

        private static string BuildAccionNombre(string accion)
        {
            var controller = nameof(ClienteController).Replace("Controller", string.Empty).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(accion))
            {
                return controller;
            }
            var normalized = accion.Contains('.')
                ? accion
                : $"{controller}.{accion}";
            return normalized.ToUpperInvariant();
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
        private static string? BuildDireccion(string? calle, string? numero, string? localidad)
        {
            calle = calle?.Trim();
            numero = numero?.Trim();
            localidad = localidad?.Trim();
            if (string.IsNullOrWhiteSpace(calle) && string.IsNullOrWhiteSpace(numero) && string.IsNullOrWhiteSpace(localidad))
                return null;

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(calle))
            {
                parts.Add(calle);
            }
            if (!string.IsNullOrWhiteSpace(numero))
            {
                if (parts.Count > 0)
                    parts[parts.Count - 1] = parts[parts.Count - 1] + " " + numero;
                else
                    parts.Add(numero);
            }
            if (!string.IsNullOrWhiteSpace(localidad))
            {
                parts.Add(localidad);
            }
            return string.Join(", ", parts);
        }
    }
}






