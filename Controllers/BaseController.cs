using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Data;

namespace mi_ferreteria.Controllers
{
    public abstract class BaseController : Controller
    {
        private readonly IAuditoriaRepository _auditoriaRepo;

        protected BaseController(IAuditoriaRepository auditoriaRepo)
        {
            _auditoriaRepo = auditoriaRepo;
        }

        // Extrae el ID y nombre del usuario autenticado desde los claims. Retorna false si no hay sesión válida.
        protected bool TryGetAuditoriaUsuario(out int userId, out string usuarioNombre)
        {
            usuarioNombre = User?.Identity?.Name ?? "Usuario desconocido";
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out userId) || userId <= 0)
                return false;
            if (string.IsNullOrWhiteSpace(usuarioNombre))
                usuarioNombre = $"Usuario {userId}";
            return true;
        }

        // Persiste un registro de auditoría para el usuario actual. Si no hay sesión válida, no registra nada.
        protected void RegistrarAuditoria(string accion, string detalle)
        {
            if (!TryGetAuditoriaUsuario(out var userId, out var usuarioNombre)) return;
            var finalAccion = BuildAccionNombre(accion);
            _auditoriaRepo.Registrar(userId, usuarioNombre, finalAccion, detalle);
            HttpContext.Items["AuditLogged"] = true;
        }

        // Construye el nombre normalizado de la acción en formato "CONTROLADOR.ACCION" para el registro de auditoría.
        protected string BuildAccionNombre(string accion)
        {
            var controller = GetType().Name.Replace("Controller", string.Empty).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(accion)) return controller;
            var normalized = accion.Contains('.') ? accion : $"{controller}.{accion}";
            return normalized.ToUpperInvariant();
        }
    }
}
