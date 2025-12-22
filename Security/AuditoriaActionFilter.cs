using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;

namespace mi_ferreteria.Security
{
    public class AuditoriaActionFilter : IAsyncActionFilter
    {
        private readonly IAuditoriaRepository _auditoriaRepository;
        private readonly ILogger<AuditoriaActionFilter> _logger;
        private static readonly HashSet<string> SensitiveKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "confirmPassword",
            "token",
            "currentPassword",
            "newPassword"
        };

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
                if (context.HttpContext.Items.ContainsKey("AuditLogged"))
                {
                    return;
                }

                var method = context.HttpContext.Request.Method;
                if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (context.Controller is Controller mvcController && !mvcController.ModelState.IsValid)
                {
                    return;
                }

                if (resultContext.Exception != null && !resultContext.ExceptionHandled)
                {
                    return;
                }

                if (resultContext.Result is IStatusCodeActionResult statusResult &&
                    statusResult.StatusCode.HasValue && statusResult.StatusCode.Value >= 400)
                {
                    return;
                }

                if (resultContext.HttpContext.Response?.StatusCode >= 400)
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

                var controllerName = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c : context.Controller.GetType().Name;
                var action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var a) ? a : "Accion";
                var accion = $"{controllerName}.{action}".ToUpperInvariant();
                var nombre = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Email)?.Value ?? $"Usuario {uid}";
                var detalle = BuildDetalle(context, method);

                _auditoriaRepository.Registrar(uid, nombre, accion, detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al registrar auditoria de accion {Metodo} {Ruta}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            }
        }

        private static string BuildDetalle(ActionExecutingContext context, string method)
        {
            if (context.HttpContext.Items.TryGetValue("AuditDetail", out var existing) && existing is string custom && !string.IsNullOrWhiteSpace(custom))
            {
                return custom;
            }

            var args = context.ActionArguments;
            if (args == null || args.Count == 0)
            {
                return $"{method.ToUpperInvariant()} {context.HttpContext.Request.Path}";
            }

            var parts = new List<string>();
            foreach (var kvp in args)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || SensitiveKeys.Contains(kvp.Key))
                {
                    continue;
                }
                var resumen = DescribeValue(kvp.Value, kvp.Key);
                if (string.IsNullOrEmpty(resumen))
                {
                    continue;
                }
                parts.Add($"{kvp.Key}={resumen}");
                if (parts.Count >= 6)
                {
                    break;
                }
            }

            if (parts.Count == 0)
            {
                return $"{method.ToUpperInvariant()} {context.HttpContext.Request.Path}";
            }
            return string.Join(", ", parts);
        }

        private static string DescribeValue(object? value, string? currentKey = null)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return string.Empty;
                }
                return $"\"{Truncate(s.Trim(), 80)}\"";
            }

            var type = value.GetType();
            if (IsSimple(type))
            {
                return Truncate(Convert.ToString(value) ?? string.Empty, 80);
            }

            if (value is IEnumerable enumerable)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    if (items.Count >= 3) break;
                    var desc = DescribeValue(item);
                    if (!string.IsNullOrEmpty(desc))
                    {
                        items.Add(desc);
                    }
                }
                if (items.Count == 0)
                {
                    return string.Empty;
                }
                return $"[{string.Join(", ", items)}]";
            }

            var props = type
                .GetProperties()
                .Where(p => p.CanRead && IsSimple(p.PropertyType) && !SensitiveKeys.Contains(p.Name))
                .Take(4)
                .ToList();
            if (props.Count == 0)
            {
                return type.Name;
            }

            var builder = new StringBuilder();
            builder.Append("{");
            var added = 0;
            foreach (var prop in props)
            {
                var propValue = prop.GetValue(value);
                if (propValue == null)
                {
                    continue;
                }
                var desc = DescribeValue(propValue, prop.Name);
                if (string.IsNullOrEmpty(desc))
                {
                    continue;
                }
                if (added > 0) builder.Append(", ");
                builder.Append(prop.Name);
                builder.Append("=");
                builder.Append(desc);
                added++;
            }

            if (added == 0)
            {
                return type.Name;
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive
                   || type.IsEnum
                   || type == typeof(string)
                   || type == typeof(decimal)
                   || type == typeof(DateTime)
                   || type == typeof(DateTimeOffset)
                   || type == typeof(Guid);
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : $"{value.Substring(0, max)}...";
        }
    }
}
