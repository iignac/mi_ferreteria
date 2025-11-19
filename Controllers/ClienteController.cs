using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace mi_ferreteria.Controllers
{
    public class ClienteController : Controller
    {
        private readonly IClienteRepository _repo;
        private readonly ILogger<ClienteController> _logger;

        public ClienteController(IClienteRepository repo, ILogger<ClienteController> logger)
        {
            _repo = repo;
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
                saldoDisponible = c.LimiteCredito - saldoActual.GetValueOrDefault();
            }
            ViewBag.SaldoActual = saldoActual;
            ViewBag.SaldoDisponible = saldoDisponible;
            return View(c);
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

                _repo.Update(cliente);
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
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al activar cliente {ClienteId}", id);
                return Problem("Ocurrió un error al activar el cliente.");
            }
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
