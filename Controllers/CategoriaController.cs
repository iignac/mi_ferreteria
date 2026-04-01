using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.Controllers
{
    public class CategoriaController : BaseController
    {
        private readonly ICategoriaRepository _repo;
        private readonly ILogger<CategoriaController> _logger;

        public CategoriaController(ICategoriaRepository repo, IAuditoriaRepository auditoriaRepo, ILogger<CategoriaController> logger)
            : base(auditoriaRepo)
        {
            _repo = repo;
            _logger = logger;
        }

        // Lista las categorías paginadas con soporte de búsqueda y ordenamiento por columnas.
        public IActionResult Index(string? q = null, string? sort = null, int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                var validSorts = new System.Collections.Generic.HashSet<string>(new[] {
                    "id_asc","id_desc","nombre_asc","nombre_desc","activo_asc","activo_desc"
                }, System.StringComparer.OrdinalIgnoreCase);
                sort = string.IsNullOrWhiteSpace(sort) ? "id_asc" : sort.Trim().ToLowerInvariant();
                if (!validSorts.Contains(sort)) sort = "id_desc";

                int total;
                int totalPages;
                IEnumerable<Categoria> list;
                if (!string.IsNullOrWhiteSpace(q))
                {
                    total = _repo.CountSearch(q);
                    totalPages = (int)System.Math.Ceiling(total / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page > totalPages) page = totalPages;
                    list = _repo.SearchPageSorted(q, page, pageSize, sort).ToList();
                }
                else
                {
                    total = _repo.CountAll();
                    totalPages = (int)System.Math.Ceiling(total / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page > totalPages) page = totalPages;
                    list = _repo.GetPageSorted(page, pageSize, sort).ToList();
                }
                var catNames = _repo.GetAll().ToDictionary(c => c.Id, c => c.Nombre);
                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = total;
                ViewBag.TotalPages = totalPages;
                ViewBag.Sort = sort;
                ViewBag.Query = q;
                ViewBag.CategoriaNames = catNames;
                return View(list);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar categorías");
                return View(Enumerable.Empty<Categoria>());
            }
        }

        // Muestra el formulario de alta de categoría. Soporta respuesta parcial para carga dentro de un modal AJAX.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Create()
        {
            ViewBag.Categorias = _repo.GetAll().Where(c => c.Activo);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView(new Categoria());
            return View(new Categoria());
        }

        // Valida y persiste la nueva categoría. Verifica nombre único y que la categoría padre exista y esté activa.
        [HttpPost]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Create([Required] string Nombre, long? IdPadre, string? Descripcion)
        {
            ViewBag.Categorias = _repo.GetAll().Where(c => c.Activo);
            try
            {
                if (string.IsNullOrWhiteSpace(Nombre))
                {
                    ModelState.AddModelError("Nombre", "El nombre es obligatorio");
                }
                if (IdPadre.HasValue)
                {
                    var padre = _repo.GetById(IdPadre.Value);
                    if (padre == null || !padre.Activo)
                    {
                        ModelState.AddModelError("IdPadre", "La categoría padre no existe o está inactiva");
                    }
                }
                if (!ModelState.IsValid)
                {
                    var c = new Categoria { Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion };
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(c);
                    return View(c);
                }
                if (_repo.NombreExists(Nombre))
                {
                    ModelState.AddModelError("Nombre", "La categoría ya existe");
                    var c = new Categoria { Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion };
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(c);
                    return View(c);
                }
                var nueva = new Categoria { Nombre = Nombre!, IdPadre = IdPadre, Descripcion = Descripcion, Activo = true };
                _repo.Add(nueva);
                RegistrarAuditoria(nameof(Create), $"Alta de categoria #{nueva.Id}: {nueva.Nombre}");
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría");
                ModelState.AddModelError(string.Empty, "Error al crear categoría");
                var c = new Categoria { Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion };
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(c);
                return View(c);
            }
        }

        // Muestra el formulario de edición con los datos actuales de la categoría. Soporta carga por modal AJAX.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Edit(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            ViewBag.Categorias = _repo.GetAll().Where(x => x.Id != id && x.Activo);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(c);
            return View(c);
        }

        // Valida y actualiza la categoría. Evita que sea su propia padre y verifica nombre único excluyendo la propia.
        [HttpPost]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Edit(long Id, [Required] string Nombre, long? IdPadre, string? Descripcion, bool Activo = true)
        {
            ViewBag.Categorias = _repo.GetAll().Where(x => x.Id != Id && x.Activo);
            try
            {
                var anterior = _repo.GetById(Id);
                if (string.IsNullOrWhiteSpace(Nombre))
                {
                    ModelState.AddModelError("Nombre", "El nombre es obligatorio");
                }
                if (IdPadre.HasValue)
                {
                    if (IdPadre.Value == Id)
                    {
                        ModelState.AddModelError("IdPadre", "La categoría no puede ser su propia padre");
                    }
                    var padre = _repo.GetById(IdPadre.Value);
                    if (padre == null || !padre.Activo)
                    {
                        ModelState.AddModelError("IdPadre", "La categoría padre no existe o está inactiva");
                    }
                }
                if (!ModelState.IsValid)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                    return View(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                }
                if (_repo.NombreExists(Nombre, Id))
                {
                    ModelState.AddModelError("Nombre", "La categoría ya existe");
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(new Categoria { Id = Id, Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                    return View(new Categoria { Id = Id, Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                }
                var nueva = new Categoria { Id = Id, Nombre = Nombre!, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo };
                _repo.Update(nueva);
                if (anterior != null)
                {
                    RegistrarAuditoria(nameof(Edit), $"Actualizacion de categoria #{Id}: nombre '{anterior.Nombre}' -> '{nueva.Nombre}', activo {anterior.Activo} -> {nueva.Activo}, descripcion '{anterior.Descripcion}' -> '{nueva.Descripcion}'");
                }
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = true });
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría {CategoriaId}", Id);
                ModelState.AddModelError(string.Empty, "Error al actualizar categoría");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                return View(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
            }
        }

        // Muestra la pantalla de confirmación antes de eliminar la categoría.
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Delete(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            return View(c);
        }

        // Ejecuta el borrado lógico de la categoría (la marca como inactiva) y registra la acción en auditoría.
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult DeleteConfirmed(long id)
        {
            try
            {
                var anterior = _repo.GetById(id);
                _repo.Delete(id);
                if (anterior != null)
                {
                    RegistrarAuditoria("Delete", $"Eliminacion de categoria #{id}: {anterior.Nombre}");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría {CategoriaId}", id);
                return Problem("Ocurrió un error al eliminar la categoría.");
            }
        }

        // Reactiva una categoría previamente dada de baja y registra la acción en auditoría.
        [HttpPost]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Activate(long id)
        {
            try
            {
                var anterior = _repo.GetById(id);
                _repo.Activate(id);
                if (anterior != null)
                {
                    RegistrarAuditoria(nameof(Activate), $"Reactivacion de categoria #{id}: estado previo activo={anterior.Activo}");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al activar categorA-a {CategoriaId}", id);
                return Problem("OcurriA3 un error al activar la categorA-a.");
            }
        }

        // Elimina físicamente la categoría de la base de datos. Operación irreversible.
        [HttpPost]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult HardDelete(long id)
        {
            try
            {
                var anterior = _repo.GetById(id);
                _repo.HardDelete(id);
                if (anterior != null)
                {
                    RegistrarAuditoria(nameof(HardDelete), $"Eliminacion definitiva de categoria #{id}: {anterior.Nombre}");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar fA-sicamente categorA-a {CategoriaId}", id);
                return Problem("OcurriA3 un error al eliminar fA-sicamente la categorA-a.");
            }
        }

    }
}
