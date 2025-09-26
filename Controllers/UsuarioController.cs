using mi_ferreteria.Data;
using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace mi_ferreteria.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRolRepository _rolRepository;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(IUsuarioRepository usuarioRepository, IRolRepository rolRepository, ILogger<UsuarioController> logger)
        {
            _usuarioRepository = usuarioRepository;
            _rolRepository = rolRepository;
            _logger = logger;
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
                return Problem("Ocurrió un error al obtener los usuarios.");
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
                _logger.LogError(ex, "Error preparando formulario de creación de usuario");
                return Problem("Ocurrió un error al preparar el formulario.");
            }
        }

        [HttpPost]
        public IActionResult Create(UsuarioFormViewModel model)
        {
            try
            {
                model.RolesDisponibles = _rolRepository.GetAll();
                if (ModelState.IsValid)
                {
                    // Verificación de email único
                    if (_usuarioRepository.EmailExists(model.Email))
                    {
                        ModelState.AddModelError("Email", "El email ya está registrado");
                        return View(model);
                    }
                    if (string.IsNullOrWhiteSpace(model.Password))
                    {
                        ModelState.AddModelError("Password", "La contraseña es obligatoria.");
                        return View(model);
                    }
                    if (model.Password != model.ConfirmPassword)
                    {
                        ModelState.AddModelError("ConfirmPassword", "Las contraseñas no coinciden.");
                        return View(model);
                    }
                    var usuario = new Usuario
                    {
                        Nombre = model.Nombre,
                        Email = model.Email,
                        Activo = model.Activo,
                        Roles = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList()
                    };
                    _usuarioRepository.Add(usuario, model.Password);
                    return RedirectToAction("Index");
                }
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario");
                return Problem("Ocurrió un error al crear el usuario.");
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
                    RolesDisponibles = _rolRepository.GetAll()
                };
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error preparando edición de usuario {UsuarioId}", id);
                return Problem("Ocurrió un error al preparar la edición.");
            }
        }

        [HttpPost]
        public IActionResult Edit(UsuarioFormViewModel model)
        {
            try
            {
                model.RolesDisponibles = _rolRepository.GetAll();
                if (ModelState.IsValid)
                {
                    // Verificación de email único (excluyendo el propio)
                    if (_usuarioRepository.EmailExists(model.Email, model.Id))
                    {
                        ModelState.AddModelError("Email", "El email ya está registrado por otro usuario");
                        return View(model);
                    }
                    var usuario = new Usuario
                    {
                        Id = model.Id,
                        Nombre = model.Nombre,
                        Email = model.Email,
                        Activo = model.Activo,
                        Roles = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList()
                    };
                    string? newPwd = null;
                    if (!string.IsNullOrWhiteSpace(model.Password) || !string.IsNullOrWhiteSpace(model.ConfirmPassword))
                    {
                        if (string.IsNullOrWhiteSpace(model.Password))
                        {
                            ModelState.AddModelError("Password", "La contraseña no puede ser vacía si desea cambiarla.");
                            return View(model);
                        }
                        if (model.Password != model.ConfirmPassword)
                        {
                            ModelState.AddModelError("ConfirmPassword", "Las contraseñas no coinciden.");
                            return View(model);
                        }
                        newPwd = model.Password;
                    }
                    _usuarioRepository.Update(usuario, newPwd);
                    return RedirectToAction("Index");
                }
                return View(model);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario {UsuarioId}", model.Id);
                return Problem("Ocurrió un error al actualizar el usuario.");
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
                _logger.LogError(ex, "Error al cargar confirmación de borrado de usuario {UsuarioId}", id);
                return Problem("Ocurrió un error al cargar la confirmación.");
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            try
            {
                _usuarioRepository.Delete(id);
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario {UsuarioId}", id);
                return Problem("Ocurrió un error al eliminar el usuario.");
            }
        }
    }
}
