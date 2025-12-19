using mi_ferreteria.Data;
using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using mi_ferreteria.Security;

namespace mi_ferreteria.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuarioController : Controller
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRolRepository _rolRepository;
        private readonly ILogger<UsuarioController> _logger;
        private readonly IAuditoriaRepository _auditoriaRepository;

        public UsuarioController(IUsuarioRepository usuarioRepository, IRolRepository rolRepository, ILogger<UsuarioController> logger, IAuditoriaRepository auditoriaRepository)
        {
            _usuarioRepository = usuarioRepository;
            _rolRepository = rolRepository;
            _logger = logger;
            _auditoriaRepository = auditoriaRepository;
        }

        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Listando usuarios");
                var usuarios = _usuarioRepository.GetAll();
                return View(usuarios);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios");
                return Problem("Ocurrio un error al obtener los usuarios.");
            }
        }

        public IActionResult Create()
        {
            try
            {
                var model = new UsuarioFormViewModel
                {
                    RolesDisponibles = _rolRepository.GetAll()
                };
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error preparando formulario de creacion de usuario");
                return Problem("Ocurrio un error al preparar el formulario.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(UsuarioFormViewModel model)
        {
            try
            {
                model.RolesDisponibles = _rolRepository.GetAll();
                model.Nombre = model.Nombre?.Trim();
                model.Email = model.Email?.Trim();
                if (ModelState.IsValid)
                {
                    if (string.IsNullOrWhiteSpace(model.Nombre))
                    {
                        ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
                        return View(model);
                    }
                    if (string.IsNullOrWhiteSpace(model.Email))
                    {
                        ModelState.AddModelError("Email", "El email es obligatorio.");
                        return View(model);
                    }
                    if (_usuarioRepository.EmailExists(model.Email))
                    {
                        ModelState.AddModelError("Email", "El email ya esta registrado");
                        return View(model);
                    }
                    if (string.IsNullOrWhiteSpace(model.Password))
                    {
                        ModelState.AddModelError("Password", "La contrasena es obligatoria.");
                        return View(model);
                    }
                    if (model.Password != model.ConfirmPassword)
                    {
                        ModelState.AddModelError("ConfirmPassword", "Las contrasenas no coinciden.");
                        return View(model);
                    }
                    if (!PasswordPolicy.IsStrong(model.Password, out var pwdMsg))
                    {
                        ModelState.AddModelError("Password", pwdMsg);
                        return View(model);
                    }

                    var rolesSeleccionados = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList();
                    var usuario = new Usuario
                    {
                        Nombre = model.Nombre,
                        Email = model.Email,
                        Activo = model.Activo,
                        Roles = rolesSeleccionados
                    };
                    _usuarioRepository.Add(usuario, model.Password);
                    RegistrarAuditoria("USUARIO_CREADO", $"Usuario #{usuario.Id}: {usuario.Nombre} ({usuario.Email}), Activo={usuario.Activo}, Roles=[{FormatearRoles(rolesSeleccionados)}]");
                    TempData["Success"] = "Usuario creado correctamente.";
                    return RedirectToAction("Index");
                }
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario");
                return Problem("Ocurrio un error al crear el usuario.");
            }
        }

        public IActionResult Edit(int id)
        {
            try
            {
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                if (usuario == null) return NotFound();
                var rolesIds = usuario.Roles.Select(r => r.Id).ToList();
                var model = new UsuarioFormViewModel
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Email = usuario.Email,
                    Activo = usuario.Activo,
                    RolesIds = rolesIds,
                    RolesDisponibles = _rolRepository.GetAll(),
                    OriginalHash = mi_ferreteria.Security.ConcurrencyToken.ComputeUsuarioHash(usuario)
                };
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error preparando edicion de usuario {UsuarioId}", id);
                return Problem("Ocurrio un error al preparar la edicion.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(UsuarioFormViewModel model)
        {
            try
            {
                model.RolesDisponibles = _rolRepository.GetAll();
                model.Nombre = model.Nombre?.Trim();
                model.Email = model.Email?.Trim();
                if (ModelState.IsValid)
                {
                    if (string.IsNullOrWhiteSpace(model.Nombre))
                    {
                        ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
                        return View(model);
                    }
                    if (string.IsNullOrWhiteSpace(model.Email))
                    {
                        ModelState.AddModelError("Email", "El email es obligatorio.");
                        return View(model);
                    }
                    var dbUsuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == model.Id);
                    if (dbUsuario == null) return NotFound();
                    var currentHash = mi_ferreteria.Security.ConcurrencyToken.ComputeUsuarioHash(dbUsuario);
                    if (!string.IsNullOrEmpty(model.OriginalHash) && !string.Equals(model.OriginalHash, currentHash, System.StringComparison.Ordinal))
                    {
                        Response.StatusCode = 409;
                        ModelState.AddModelError(string.Empty, "El usuario fue modificado por otro proceso. Recarga la pagina.");
                        return View(model);
                    }
                    if (_usuarioRepository.EmailExists(model.Email, model.Id))
                    {
                        ModelState.AddModelError("Email", "El email ya esta registrado por otro usuario");
                        return View(model);
                    }
                    var rolesSeleccionados = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList();
                    var usuario = new Usuario
                    {
                        Id = model.Id,
                        Nombre = model.Nombre,
                        Email = model.Email,
                        Activo = model.Activo,
                        Roles = rolesSeleccionados
                    };
                    string? newPwd = null;
                    if (!string.IsNullOrWhiteSpace(model.Password) || !string.IsNullOrWhiteSpace(model.ConfirmPassword))
                    {
                        if (string.IsNullOrWhiteSpace(model.Password))
                        {
                            ModelState.AddModelError("Password", "La contrasena no puede ser vacia si desea cambiarla.");
                            return View(model);
                        }
                        if (model.Password != model.ConfirmPassword)
                        {
                            ModelState.AddModelError("ConfirmPassword", "Las contrasenas no coinciden.");
                            return View(model);
                        }
                        if (!PasswordPolicy.IsStrong(model.Password, out var pwdMsg))
                        {
                            ModelState.AddModelError("Password", pwdMsg);
                            return View(model);
                        }
                        newPwd = model.Password;
                    }
                    _usuarioRepository.Update(usuario, newPwd);
                    var rolesAntes = FormatearRoles(dbUsuario.Roles);
                    var rolesDespues = FormatearRoles(rolesSeleccionados);
                    var pwdDetalle = string.IsNullOrWhiteSpace(newPwd) ? "clave sin cambios" : "clave actualizada";
                    RegistrarAuditoria("USUARIO_EDITADO",
                        $"Usuario #{usuario.Id}: nombre '{dbUsuario.Nombre}' -> '{usuario.Nombre}', email '{dbUsuario.Email}' -> '{usuario.Email}', activo {dbUsuario.Activo} -> {usuario.Activo}, roles [{rolesAntes}] -> [{rolesDespues}], {pwdDetalle}");
                    TempData["Success"] = "Usuario actualizado correctamente.";
                    return RedirectToAction("Index");
                }
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario {UsuarioId}", model.Id);
                return Problem("Ocurrio un error al actualizar el usuario.");
            }
        }

        public IActionResult Delete(int id)
        {
            try
            {
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                if (usuario == null) return NotFound();
                return View(usuario);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al cargar confirmacion de borrado de usuario {UsuarioId}", id);
                return Problem("Ocurrio un error al cargar la confirmacion.");
            }
        }

        public IActionResult Details(int id)
        {
            try
            {
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                if (usuario == null) return NotFound();
                return View(usuario);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalles de usuario {UsuarioId}", id);
                return Problem("Ocurrio un error al cargar los detalles del usuario.");
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                var nombre = usuario?.Nombre ?? ("#" + id);
                _usuarioRepository.Delete(id);
                RegistrarAuditoria("USUARIO_ELIMINADO", $"Usuario #{id}: {nombre} ({usuario?.Email ?? "sin email"})");
                TempData["Success"] = $"Usuario '{nombre}' eliminado correctamente.";
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario {UsuarioId}", id);
                return Problem("Ocurrio un error al eliminar el usuario.");
            }
        }

        private void RegistrarAuditoria(string accion, string detalle)
        {
            var userIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var nombre = User?.Identity?.Name ?? "Usuario desconocido";
            if (int.TryParse(userIdClaim, out var uid) && uid > 0)
            {
                _auditoriaRepository.Registrar(uid, nombre, accion.ToUpperInvariant(), detalle);
                HttpContext.Items["AuditLogged"] = true;
            }
        }

        private static string FormatearRoles(IEnumerable<Rol>? roles)
        {
            if (roles == null || !roles.Any()) return "Sin roles";
            return string.Join(", ", roles.Select(r => r.Nombre));
        }
    }
}
