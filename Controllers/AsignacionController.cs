using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace mi_ferreteria.Controllers
{
    public class AsignacionController : Controller
    {
        // Simulación de almacenamiento en memoria
        private static List<Usuario> usuarios = new List<Usuario>();
        private static List<Rol> roles = new List<Rol>
        {
            new Rol { Id = 1, Nombre = "Administrador", Descripcion = "Acceso total" },
            new Rol { Id = 2, Nombre = "Vendedor", Descripcion = "Solo ventas" },
            new Rol { Id = 3, Nombre = "Stock", Descripcion = "Gestión de productos y precios" }
        };
        private static List<Permiso> permisos = new List<Permiso>
        {
            new Permiso { Id = 1, Nombre = "VerReportes", Descripcion = "Puede ver reportes" },
            new Permiso { Id = 2, Nombre = "ModificarPrecios", Descripcion = "Puede modificar precios" },
            new Permiso { Id = 3, Nombre = "GestionarStock", Descripcion = "Puede gestionar stock" }
        };

        // Asignar roles a usuario
        public IActionResult AsignarRoles(int usuarioId)
        {
            var usuario = usuarios.FirstOrDefault(u => u.Id == usuarioId);
            if (usuario == null) return NotFound();
            var model = new UsuarioRolViewModel
            {
                UsuarioId = usuario.Id,
                UsuarioNombre = usuario.Nombre,
                RolesIds = usuario.Roles.Select(r => r.Id).ToList(),
                RolesDisponibles = roles
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult AsignarRoles(UsuarioRolViewModel model)
        {
            var usuario = usuarios.FirstOrDefault(u => u.Id == model.UsuarioId);
            if (usuario == null) return NotFound();
            usuario.Roles = roles.Where(r => model.RolesIds.Contains(r.Id)).ToList();
            return RedirectToAction("Index", "Usuario");
        }

        // Asignar permisos a rol
        public IActionResult AsignarPermisos(int rolId)
        {
            var rol = roles.FirstOrDefault(r => r.Id == rolId);
            if (rol == null) return NotFound();
            var model = new RolPermisoViewModel
            {
                RolId = rol.Id,
                RolNombre = rol.Nombre,
                PermisosIds = rol.Permisos.Select(p => p.Id).ToList(),
                PermisosDisponibles = permisos
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult AsignarPermisos(RolPermisoViewModel model)
        {
            var rol = roles.FirstOrDefault(r => r.Id == model.RolId);
            if (rol == null) return NotFound();
            rol.Permisos = permisos.Where(p => model.PermisosIds.Contains(p.Id)).ToList();
            return RedirectToAction("Index", "Usuario");
        }
    }
}
