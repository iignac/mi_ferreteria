using mi_ferreteria.Data;
using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using mi_ferreteria.Security;
using System.Collections.Generic;

namespace mi_ferreteria.Controllers
{
    [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Ver)]
    public class UsuarioController : Controller
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRolRepository _rolRepository;
        private readonly ILogger<UsuarioController> _logger;
        private readonly IAuditoriaRepository _auditoriaRepository;
        private readonly IPermisoRepository _permisoRepository;
        private const string RolAdministradorNombre = "ADMINISTRADOR";
        private const string RolVendedorNombre = "VENDEDOR";
        private const string RolStockNombre = "STOCK";

        public UsuarioController(IUsuarioRepository usuarioRepository, IRolRepository rolRepository, ILogger<UsuarioController> logger, IAuditoriaRepository auditoriaRepository, IPermisoRepository permisoRepository)
        {
            _usuarioRepository = usuarioRepository;
            _rolRepository = rolRepository;
            _logger = logger;
            _auditoriaRepository = auditoriaRepository;
            _permisoRepository = permisoRepository;
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

        [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Gestionar)]
        public IActionResult Create()
        {
            try
            {
                var model = new UsuarioFormViewModel
                {
                    RolesDisponibles = _rolRepository.GetAll(),
                    TodosLosPermisos = _permisoRepository.GetAll()
                };
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView(model);
                }
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error preparando formulario de creacion de usuario");
                return Problem("Ocurrio un error al preparar el formulario.");
            }
        }

        [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Gestionar)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(UsuarioFormViewModel model)
        {
            try
            {
                model.RolesDisponibles = _rolRepository.GetAll();
                model.RolesIds ??= new List<int>();
                model.PermisosIds ??= new List<int>();
                model.TodosLosPermisos = _permisoRepository.GetAll();
                var permisosHeredados = _permisoRepository.GetByRolIds(model.RolesIds)
                    .Select(p => p.Nombre)
                    .ToList();
                model.PermisosHeredados = permisosHeredados;
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
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    if (string.IsNullOrWhiteSpace(model.Password))
                    {
                        ModelState.AddModelError("Password", "La contrasena es obligatoria.");
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    if (model.Password != model.ConfirmPassword)
                    {
                        ModelState.AddModelError("ConfirmPassword", "Las contrasenas no coinciden.");
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    if (!PasswordPolicy.IsStrong(model.Password, out var pwdMsg))
                    {
                        ModelState.AddModelError("Password", pwdMsg);
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }

                    var rolesSeleccionados = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList();
                    if (!RolesCompatibles(rolesSeleccionados))
                    {
                        return View(model);
                    }
                    var usuario = new Usuario
                    {
                        Nombre = model.Nombre,
                        Email = model.Email,
                        Activo = model.Activo,
                        Roles = rolesSeleccionados
                    };
                    _usuarioRepository.Add(usuario, model.Password);
                    _permisoRepository.AsignarPermisosDirectos(usuario.Id, model.PermisosIds);
                    var permisosDirectosNombres = model.TodosLosPermisos
                        .Where(p => model.PermisosIds.Contains(p.Id))
                        .Select(p => p.Nombre)
                        .ToList();
                    var detallePermisos = permisosDirectosNombres.Any()
                        ? string.Join(", ", permisosDirectosNombres)
                        : "Sin permisos directos";
                    RegistrarAuditoria(nameof(Create), $"Alta de usuario #{usuario.Id}: {usuario.Nombre} ({usuario.Email}), Activo={usuario.Activo}, Roles=[{FormatearRoles(rolesSeleccionados)}], PermisosDirectos=[{detallePermisos}]");
                    TempData["Success"] = "Usuario creado correctamente.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, redirectUrl = Url.Action("Index") });
                    }
                    return RedirectToAction("Index");
                }
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario");
                return Problem("Ocurrio un error al crear el usuario.");
            }
        }

        [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Gestionar)]
        public IActionResult Edit(int id)
        {
            try
            {
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                if (usuario == null) return NotFound();
                var rolesIds = usuario.Roles.Select(r => r.Id).ToList();
                var permisosHeredados = _permisoRepository.GetByRolIds(rolesIds).Select(p => p.Nombre).ToList();
                var permisosDirectos = _permisoRepository.GetByUsuarioIdDirecto(id);
                var todosLosPermisosDb = _permisoRepository.GetAll();
                
                var permisosDirectosIds = todosLosPermisosDb
                    .Where(p => permisosDirectos.Any(pd => pd.Id == p.Id))
                    .Select(p => p.Id)
                    .ToList();

                var model = new UsuarioFormViewModel
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Email = usuario.Email,
                    Activo = usuario.Activo,
                    RolesIds = rolesIds,
                    RolesDisponibles = _rolRepository.GetAll(),
                    PermisosHeredados = permisosHeredados,
                    PermisosIds = permisosDirectosIds,
                    TodosLosPermisos = todosLosPermisosDb,
                    OriginalHash = mi_ferreteria.Security.ConcurrencyToken.ComputeUsuarioHash(usuario)
                };
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView(model);
                }
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error preparando edicion de usuario {UsuarioId}", id);
                return Problem("Ocurrio un error al preparar la edicion.");
            }
        }

        [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Gestionar)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(UsuarioFormViewModel model)
        {
            try
            {
                model.RolesDisponibles = _rolRepository.GetAll();
                model.RolesIds ??= new List<int>();
                model.PermisosIds ??= new List<int>();
                model.TodosLosPermisos = _permisoRepository.GetAll();
                var permisosHeredados = _permisoRepository.GetByRolIds(model.RolesIds).Select(p => p.Nombre).ToList();
                model.PermisosHeredados = permisosHeredados;
                
                model.Nombre = model.Nombre?.Trim();
                model.Email = model.Email?.Trim();
                if (ModelState.IsValid)
                {
                    if (string.IsNullOrWhiteSpace(model.Nombre))
                    {
                        ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    if (string.IsNullOrWhiteSpace(model.Email))
                    {
                        ModelState.AddModelError("Email", "El email es obligatorio.");
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    var dbUsuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == model.Id);
                    if (dbUsuario == null) return NotFound();
                    var currentHash = mi_ferreteria.Security.ConcurrencyToken.ComputeUsuarioHash(dbUsuario);
                    if (!string.IsNullOrEmpty(model.OriginalHash) && !string.Equals(model.OriginalHash, currentHash, System.StringComparison.Ordinal))
                    {
                        Response.StatusCode = 409;
                        ModelState.AddModelError(string.Empty, "El usuario fue modificado por otro proceso. Recarga la pagina.");
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    if (_usuarioRepository.EmailExists(model.Email, model.Id))
                    {
                        ModelState.AddModelError("Email", "El email ya esta registrado por otro usuario");
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
                    var rolesSeleccionados = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList();
                    if (!RolesCompatibles(rolesSeleccionados))
                    {
                        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                    }
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
                            return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                        }
                        if (model.Password != model.ConfirmPassword)
                        {
                            ModelState.AddModelError("ConfirmPassword", "Las contrasenas no coinciden.");
                            return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                        }
                        if (!PasswordPolicy.IsStrong(model.Password, out var pwdMsg))
                        {
                            ModelState.AddModelError("Password", pwdMsg);
                            return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
                        }
                        newPwd = model.Password;
                    }
                    _usuarioRepository.Update(usuario, newPwd);
                    
                    // Match the incoming Permiso Nombre (from PermisosIds which are actually IDs)
                    // Wait, we need to map the Permiso.Nombre to ID from the Db.
                    // The UI will post PermisoIds. If we bind by ID, we just save it.
                    var permisosDb = _permisoRepository.GetAll();
                    var savedPermisoIds = new System.Collections.Generic.List<int>();
                    if (model.PermisosIds != null)
                    {
                        foreach (var pid in model.PermisosIds)
                        {
                            savedPermisoIds.Add(pid);
                        }
                    }
                    _permisoRepository.AsignarPermisosDirectos(usuario.Id, savedPermisoIds);

                    var rolesAntes = FormatearRoles(dbUsuario.Roles);
                    var rolesDespues = FormatearRoles(rolesSeleccionados);
                    var pwdDetalle = string.IsNullOrWhiteSpace(newPwd) ? "clave sin cambios" : "clave actualizada";
                    RegistrarAuditoria(nameof(Edit),
                        $"Actualizacion de usuario #{usuario.Id}: nombre '{dbUsuario.Nombre}' -> '{usuario.Nombre}', email '{dbUsuario.Email}' -> '{usuario.Email}', activo {dbUsuario.Activo} -> {usuario.Activo}, roles [{rolesAntes}] -> [{rolesDespues}], {pwdDetalle}");
                    TempData["Success"] = "Usuario actualizado correctamente.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, redirectUrl = Url.Action("Index") });
                    }
                    return RedirectToAction("Index");
                }
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView(model) : View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario {UsuarioId}", model.Id);
                return Problem("Ocurrio un error al actualizar el usuario.");
            }
        }

        [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Gestionar)]
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

        [Authorize(Policy = mi_ferreteria.Security.Permisos.Usuarios.Gestionar)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                var nombre = usuario?.Nombre ?? ("#" + id);
                _usuarioRepository.Delete(id);
                RegistrarAuditoria("Delete", $"Eliminacion de usuario #{id}: {nombre} ({usuario?.Email ?? "sin email"})");
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
                var finalAccion = BuildAccionNombre(accion);
                _auditoriaRepository.Registrar(uid, nombre, finalAccion, detalle);
                HttpContext.Items["AuditLogged"] = true;
            }
        }

        private static string BuildAccionNombre(string accion)
        {
            var controller = nameof(UsuarioController).Replace("Controller", string.Empty).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(accion))
            {
                return controller;
            }
            var normalized = accion.Contains('.')
                ? accion
                : $"{controller}.{accion}";
            return normalized.ToUpperInvariant();
        }

        private static string FormatearRoles(IEnumerable<Rol>? roles)
        {
            if (roles == null || !roles.Any()) return "Sin roles";
            return string.Join(", ", roles.Select(r => r.Nombre));
        }

        private bool RolesCompatibles(IEnumerable<Rol> rolesSeleccionados)
        {
            if (rolesSeleccionados == null) return true;
            var rolesLista = rolesSeleccionados.ToList();
            if (!rolesLista.Any()) return true;
            var admin = rolesLista.Any(r => NombreRolCoincide(r?.Nombre, RolAdministradorNombre));
            if (!admin) return true;
            var conflictivo = rolesLista.Any(r =>
                NombreRolCoincide(r?.Nombre, RolVendedorNombre) ||
                NombreRolCoincide(r?.Nombre, RolStockNombre));
            if (!conflictivo) return true;
            ModelState.AddModelError("RolesIds", "Administrador abarca todas las funciones, por eso no se puede combinar con Vendedor ni Stock.");
            return false;
        }

        private static bool NombreRolCoincide(string? nombreRol, string esperado)
        {
            if (string.IsNullOrWhiteSpace(nombreRol)) return false;
            return string.Equals(
                nombreRol.Trim(),
                esperado,
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
