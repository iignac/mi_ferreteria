using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using System.ComponentModel.DataAnnotations;

namespace mi_ferreteria.Controllers
{
    public class CategoriaController : Controller
    {
        private readonly ICategoriaRepository _repo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly ILogger<CategoriaController> _logger;

        public CategoriaController(ICategoriaRepository repo, IAuditoriaRepository auditoriaRepo, ILogger<CategoriaController> logger)
        {
            _repo = repo;
            _auditoriaRepo = auditoriaRepo;
            _logger = logger;
        }

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

        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Create()
        {
            ViewBag.Categorias = _repo.GetAll().Where(c => c.Activo);
            return View(new Categoria());
        }

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
                    return View(c);
                }
                if (_repo.NombreExists(Nombre))
                {
                    ModelState.AddModelError("Nombre", "La categoría ya existe");
                    var c = new Categoria { Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion };
                    return View(c);
                }
                var nueva = new Categoria { Nombre = Nombre!, IdPadre = IdPadre, Descripcion = Descripcion, Activo = true };
                _repo.Add(nueva);
                RegistrarAuditoria("CREADO", $"Categoria #{nueva.Id}: {nueva.Nombre}");
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

        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Edit(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            ViewBag.Categorias = _repo.GetAll().Where(x => x.Id != id && x.Activo);
            return View(c);
        }

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
                    return View(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                }
                if (_repo.NombreExists(Nombre, Id))
                {
                    ModelState.AddModelError("Nombre", "La categoría ya existe");
                    return View(new Categoria { Id = Id, Nombre = Nombre, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
                }
                var nueva = new Categoria { Id = Id, Nombre = Nombre!, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo };
                _repo.Update(nueva);
                if (anterior != null)
                {
                    RegistrarAuditoria("EDICION", $"Categoria #{Id}: nombre '{anterior.Nombre}' -> '{nueva.Nombre}', activo {anterior.Activo} -> {nueva.Activo}, descripcion '{anterior.Descripcion}' -> '{nueva.Descripcion}'");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría {CategoriaId}", Id);
                ModelState.AddModelError(string.Empty, "Error al actualizar categoría");
                return View(new Categoria { Id = Id, Nombre = Nombre ?? string.Empty, IdPadre = IdPadre, Descripcion = Descripcion, Activo = Activo });
            }
        }

        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Delete(long id)
        {
            var c = _repo.GetById(id);
            if (c == null) return NotFound();
            return View(c);
        }

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
                    RegistrarAuditoria("ELIMINADO", $"Categoria #{id}: {anterior.Nombre}");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar categoría {CategoriaId}", id);
                return Problem("Ocurrió un error al eliminar la categoría.");
            }
        }

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
                    RegistrarAuditoria("EDICION", $"Categoria #{id}: activada (antes activo={anterior.Activo})");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al activar categorA-a {CategoriaId}", id);
                return Problem("OcurriA3 un error al activar la categorA-a.");
            }
        }

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
                    RegistrarAuditoria("ELIMINADO", $"Categoria #{id}: {anterior.Nombre} (hard delete)");
                }
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar fA-sicamente categorA-a {CategoriaId}", id);
                return Problem("OcurriA3 un error al eliminar fA-sicamente la categorA-a.");
            }
        }

        private void RegistrarAuditoria(string accion, string detalle)
        {
            var userIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var nombre = User?.Identity?.Name ?? "Usuario desconocido";
            if (int.TryParse(userIdClaim, out var uid) && uid > 0)
            {
                _auditoriaRepo.Registrar(uid, nombre, accion.ToUpperInvariant(), detalle);
                HttpContext.Items["AuditLogged"] = true;
            }
        }
    }
}
