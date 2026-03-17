using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;

namespace mi_ferreteria.Security
{
    public interface IAuthService
    {
        (bool Success, string? Error, ClaimsPrincipal? Principal) Authenticate(string email, string password);
    }

    public class AuthService : IAuthService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPermisoRepository _permisoRepository;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUsuarioRepository usuarioRepository, IPermisoRepository permisoRepository, ILogger<AuthService> logger)
        {
            _usuarioRepository = usuarioRepository;
            _permisoRepository = permisoRepository;
            _logger = logger;
        }

        public (bool Success, string? Error, ClaimsPrincipal? Principal) Authenticate(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Email y contrasena son obligatorios.", null);
            }

            try
            {
                var usuario = _usuarioRepository.GetByEmail(email, includeSecrets: true);
                if (usuario == null)
                {
                    return (false, "Credenciales invalidas.", null);
                }

                if (!usuario.Activo)
                {
                    return (false, "El usuario esta deshabilitado.", null);
                }

                if (usuario.PasswordHash == null || usuario.PasswordSalt == null)
                {
                    _logger.LogWarning("Usuario {UsuarioId} no tiene hash/salt configurado", usuario.Id);
                    return (false, "Credenciales invalidas.", null);
                }

                var passwordOk = PasswordHasher.Verify(password, usuario.PasswordSalt, usuario.PasswordHash);
                if (!passwordOk)
                {
                    return (false, "Credenciales invalidas.", null);
                }

                // Get consolidated permissions for RBAC
                var permisos = _permisoRepository.GetPermisosConsolidados(usuario.Id);

                var claims = BuildClaims(usuario, permisos);
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                return (true, null, principal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error autenticando al usuario {Email}", email);
                return (false, "Error interno al autenticar.", null);
            }
        }

        private static IEnumerable<Claim> BuildClaims(Usuario usuario, List<Permiso> permisos)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(usuario.Nombre) ? usuario.Email : usuario.Nombre),
                new Claim(ClaimTypes.Email, usuario.Email)
            };

            foreach (var rol in usuario.Roles ?? new List<Rol>())
            {
                if (!string.IsNullOrWhiteSpace(rol.Nombre))
                {
                    claims.Add(new Claim(ClaimTypes.Role, rol.Nombre));
                }
            }
            
            // Add custom Permission claims
            if (permisos != null)
            {
                foreach (var permiso in permisos)
                {
                    if (!string.IsNullOrWhiteSpace(permiso.Nombre))
                    {
                        claims.Add(new Claim("Permission", permiso.Nombre));
                    }
                }
            }

            return claims;
        }
    }
}
