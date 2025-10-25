using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.Controllers
{
    public class CategoriaController : Controller
    {
        private readonly ICategoriaRepository _repo;
        private readonly ILogger<CategoriaController> _logger;

        public CategoriaController(ICategoriaRepository repo, ILogger<CategoriaController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public IActionResult Index(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                var total = _repo.CountAll();
                var totalPages = (int)System.Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages) page = totalPages;
                var list = _repo.GetPage(page, pageSize).ToList();
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;
                return View(list);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar categorías");
                return View(Enumerable.Empty<Categoria>());
            }
        }

        public IActionResult Create()
        {
            ViewBag.Categorias = _repo.GetAll();
            return View(new Categoria());
        }

        [HttpPost]
        public IActionResult Create([Required] string Nombre, long? IdPadre, string? Descripcion)
        {
            ViewBag.Categorias = _repo.GetAll();
            try
            {
                if (string.IsNullOrWhiteSpace(Nombre))
                {
                    ModelState.AddModelError("Nombre", "El nombre es obligatorio");
                }
                if (!ModelState.IsValid)
                {
                    var c = new Categoria { Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion };
                    return View(c);
                }
                if (_repo.NombreExists(Nombre))
                {
                    ModelState.AddModelError("Nombre", "La categoría ya existe");
                    var c = new Categoria { Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion };
                    return View(c);
                }
                _repo.Add(new Categoria { Nombre = Nombre!, IdPadre = IdPadre, Descripcion = Descripcion });
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría");
                ModelState.AddModelError(string.Empty, "Error al crear categoría");
                var c = new Categoria { Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion };
                return View(c);
            }
        }

        public IActionResult Edit(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            ViewBag.Categorias = _repo.GetAll().Where(x => x.Id != id);
            return View(c);
        }

        [HttpPost]
        public IActionResult Edit(long Id, [Required] string Nombre, long? IdPadre, string? Descripcion)
        {
            ViewBag.Categorias = _repo.GetAll().Where(x => x.Id != Id);
            try
            {
                if (string.IsNullOrWhiteSpace(Nombre))
                {
                    ModelState.AddModelError("Nombre", "El nombre es obligatorio");
                }
                if (!ModelState.IsValid)
                {
                    return View(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion });
                }
                if (_repo.NombreExists(Nombre, Id))
                {
                    ModelState.AddModelError("Nombre", "La categoría ya existe");
                    return View(new Categoria { Id = Id, Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion });
                }
                _repo.Update(new Categoria { Id = Id, Nombre = Nombre!, IdPadre = IdPadre, Descripcion = Descripcion });
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría {CategoriaId}", Id);
                ModelState.AddModelError(string.Empty, "Error al actualizar categoría");
                return View(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion });
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
                _repo.Delete(id);
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría {CategoriaId}", id);
                return Problem("Ocurrió un error al eliminar la categoría.");
            }
        }
    }
}
