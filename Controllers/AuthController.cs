using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Security;
using mi_ferreteria.ViewModels;

namespace mi_ferreteria.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IAuditoriaRepository _auditoriaRepository;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, IAuditoriaRepository auditoriaRepository)
        {
            _authService = authService;
            _logger = logger;
            _auditoriaRepository = auditoriaRepository;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var authResult = _authService.Authenticate(model.Email, model.Password);
            if (!authResult.Success || authResult.Principal == null)
            {
                ModelState.AddModelError(string.Empty, authResult.Error ?? "No fue posible iniciar sesion.");
                return View(model);
            }

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 12 : 4),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authResult.Principal, authProperties);
            _logger.LogInformation("Usuario {Email} inicio sesion", model.Email);
            RegistrarAuditoria(authResult.Principal, "LOGIN", $"Inicio de sesion de {GetNombre(authResult.Principal)} (ID {GetUserId(authResult.Principal)}, Email {model.Email}).");

            if (authResult.Principal?.IsInRole("Administrador") == true)
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            if (authResult.Principal?.IsInRole("Vendedor") == true)
            {
                return RedirectToAction("Index", "Venta");
            }
            if (authResult.Principal?.IsInRole("Stock") == true)
            {
                return RedirectToAction("Index", "Stock");
            }

            return RedirectToLocal(model.ReturnUrl);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            RegistrarAuditoria(HttpContext.User, "LOGOUT", $"Cierre de sesion de {GetNombre(HttpContext.User)} (ID {GetUserId(HttpContext.User)}).");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private void RegistrarAuditoria(ClaimsPrincipal? principal, string accion, string detalle)
        {
            if (principal == null) return;
            var uid = GetUserId(principal);
            if (uid <= 0) return;
            var nombre = GetNombre(principal);
            _auditoriaRepository.Registrar(uid, nombre, accion.ToUpperInvariant(), detalle);
            HttpContext.Items["AuditLogged"] = true;
        }

        private static int GetUserId(ClaimsPrincipal? principal)
        {
            var uidClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(uidClaim, out var uid) ? uid : 0;
        }

        private static string GetNombre(ClaimsPrincipal? principal)
        {
            return principal?.Identity?.Name ?? principal?.FindFirst(ClaimTypes.Email)?.Value ?? "Usuario desconocido";
        }
    }
}
