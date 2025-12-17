using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;

namespace mi_ferreteria.Security
{
    public class AuditoriaActionFilter : IAsyncActionFilter
    {
        private readonly IAuditoriaRepository _auditoriaRepository;
        private readonly ILogger<AuditoriaActionFilter> _logger;

        public AuditoriaActionFilter(IAuditoriaRepository auditoriaRepository, ILogger<AuditoriaActionFilter> logger)
        {
            _auditoriaRepository = auditoriaRepository;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var resultContext = await next();

            try
            {
                var method = context.HttpContext.Request.Method;
                if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (resultContext.Exception != null && !resultContext.ExceptionHandled)
                {
                    return;
                }

                var user = context.HttpContext.User;
                if (user?.Identity?.IsAuthenticated != true)
                {
                    return;
                }

                var uidClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(uidClaim, out var uid) || uid <= 0)
                {
                    return;
                }

                var controller = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c : context.Controller.GetType().Name;
                var action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var a) ? a : "Accion";
                var accion = $"{controller}.{action}".ToUpperInvariant();
                var nombre = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Email)?.Value ?? $"Usuario {uid}";
                var detalle = $"{method.ToUpperInvariant()} {context.HttpContext.Request.Path}";

                _auditoriaRepository.Registrar(uid, nombre, accion, detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al registrar auditoria de accion {Metodo} {Ruta}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            }
        }
    }
}
