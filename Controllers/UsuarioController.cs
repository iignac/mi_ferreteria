using mi_ferreteria.Data;
using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Models;
using mi_ferreteria.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace mi_ferreteria.Controllers
{
    public class UsuarioController : Controller
    {
        // Simulación de almacenamiento en memoria
        private readonly UsuarioRepository _usuarioRepository;
        private readonly RolRepository _rolRepository;

        public UsuarioController(UsuarioRepository usuarioRepository, RolRepository rolRepository)
        {
            _usuarioRepository = usuarioRepository;
            _rolRepository = rolRepository;
        }
        
        public IActionResult Index()
        {
            var usuarios = _usuarioRepository.GetAll();
            return View(usuarios);
        }

        public IActionResult Create()
        {
            var model = new UsuarioFormViewModel
            {
                RolesDisponibles = _rolRepository.GetAll()
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult Create(UsuarioFormViewModel model)
        {
            model.RolesDisponibles = _rolRepository.GetAll();
            if (ModelState.IsValid)
            {
                var usuario = new Usuario
                {
                    Nombre = model.Nombre,
                    Email = model.Email,
                    Activo = model.Activo,
                    Roles = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList()
                };
                _usuarioRepository.Add(usuario);
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public IActionResult Edit(int id)
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

        [HttpPost]
        public IActionResult Edit(UsuarioFormViewModel model)
        {
            model.RolesDisponibles = _rolRepository.GetAll();
            if (ModelState.IsValid)
            {
                var usuario = new Usuario
                {
                    Id = model.Id,
                    Nombre = model.Nombre,
                    Email = model.Email,
                    Activo = model.Activo,
                    Roles = _rolRepository.GetAll().Where(r => model.RolesIds.Contains(r.Id)).ToList()
                };
                _usuarioRepository.Update(usuario);
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public IActionResult Delete(int id)
        {
            var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
            if (usuario == null) return NotFound();
            return View(usuario);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            _usuarioRepository.Delete(id);
            return RedirectToAction("Index");
        }
    }
}
