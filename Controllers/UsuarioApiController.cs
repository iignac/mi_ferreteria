using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using System.Linq;
using System.Collections.Generic;
using mi_ferreteria.Dtos;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Security;

namespace mi_ferreteria.Controllers
{
    [ApiController]
    [Route("api/usuarios")]
    public class UsuarioApiController : ControllerBase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRolRepository _rolRepository;
        private readonly ILogger<UsuarioApiController> _logger;

        public UsuarioApiController(IUsuarioRepository usuarioRepository, IRolRepository rolRepository, ILogger<UsuarioApiController> logger)
        {
            _usuarioRepository = usuarioRepository;
            _rolRepository = rolRepository;
            _logger = logger;
        }

        [HttpGet("listar-usuarios")]
        public ActionResult<IEnumerable<UsuarioResponseDto>> GetAll()
        {
            try
            {
                _logger.LogInformation("GET listar-usuarios");
                var usuarios = _usuarioRepository.GetAll();
                var result = usuarios.Select(u => new UsuarioResponseDto
                {
                    Id = u.Id,
                    Nombre = u.Nombre,
                    Email = u.Email,
                    Activo = u.Activo,
                    Roles = (u.Roles ?? new List<Rol>())
                        .Select(r => new RolSimpleDto { Id = r.Id, Nombre = r.Nombre, Descripcion = r.Descripcion })
                        .ToList()
                });
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en listar-usuarios");
                return StatusCode(500, "Error interno al listar usuarios");
            }
        }

        [HttpGet("obtener-usuario/{id:int}")]
        public ActionResult<UsuarioResponseDto> GetById(int id)
        {
            try
            {
                _logger.LogInformation("GET obtener-usuario {UsuarioId}", id);
                var usuario = _usuarioRepository.GetAll().FirstOrDefault(u => u.Id == id);
                if (usuario == null) return NotFound();
                var dto = new UsuarioResponseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Email = usuario.Email,
                    Activo = usuario.Activo,
                    Roles = (usuario.Roles ?? new List<Rol>())
                        .Select(r => new RolSimpleDto { Id = r.Id, Nombre = r.Nombre, Descripcion = r.Descripcion })
                        .ToList()
                };
                return Ok(dto);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en obtener-usuario {UsuarioId}", id);
                return StatusCode(500, "Error interno al obtener usuario");
            }
        }

        [HttpPost("crear-usuario")]
        public ActionResult<UsuarioResponseDto> Create([FromBody] UsuarioCreateDto dto)
        {
            try
            {
                _logger.LogInformation("POST crear-usuario");
                if (!ModelState.IsValid)
                {
                    return ValidationProblem(ModelState);
                }
                if (_usuarioRepository.EmailExists(dto.Email))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        { "Email", new[] { "El email ya esta registrado" } }
                    }));
                }
                var rolesSeleccionados = _rolRepository.GetAll()
                    .Where(r => (dto.RolesIds ?? new List<int>()).Contains(r.Id))
                    .ToList();

                if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password != dto.ConfirmPassword)
                {
                    return BadRequest("La contrasena es obligatoria y debe coincidir con la confirmacion.");
                }

                if (!PasswordPolicy.IsStrong(dto.Password, out var pwdMsg))
                {
                    return BadRequest(pwdMsg);
                }

                var usuario = new Usuario
                {
                    Nombre = dto.Nombre,
                    Email = dto.Email,
                    Activo = dto.Activo,
                    Roles = rolesSeleccionados
                };

                _usuarioRepository.Add(usuario, dto.Password);

                var response = new UsuarioResponseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Email = usuario.Email,
                    Activo = usuario.Activo,
                    Roles = rolesSeleccionados.Select(r => new RolSimpleDto { Id = r.Id, Nombre = r.Nombre, Descripcion = r.Descripcion }).ToList()
                };

                return Created($"/api/usuarios", response);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en crear-usuario {@Dto}", dto);
                return StatusCode(500, "Error interno al crear usuario");
            }
        }

        [HttpPut("actualizar-usuario/{id:int}")]
        public IActionResult Update(int id, [FromBody] UsuarioUpdateDto dto)
        {
            try
            {
                if (id != dto.Id) return BadRequest("El ID de la ruta no coincide con el del cuerpo.");
                if (!ModelState.IsValid)
                {
                    return ValidationProblem(ModelState);
                }
                if (_usuarioRepository.EmailExists(dto.Email, dto.Id))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        { "Email", new[] { "El email ya esta registrado por otro usuario" } }
                    }));
                }

                _logger.LogInformation("PUT actualizar-usuario {UsuarioId}", id);

                var rolesSeleccionados = _rolRepository.GetAll()
                    .Where(r => (dto.RolesIds ?? new List<int>()).Contains(r.Id))
                    .ToList();

                var usuario = new Usuario
                {
                    Id = dto.Id,
                    Nombre = dto.Nombre,
                    Email = dto.Email,
                    Activo = dto.Activo,
                    Roles = rolesSeleccionados
                };

                string? newPwd = null;
                if (!string.IsNullOrWhiteSpace(dto.Password) || !string.IsNullOrWhiteSpace(dto.ConfirmPassword))
                {
                    if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password != dto.ConfirmPassword)
                    {
                        return BadRequest("Las contrasenas no coinciden o estan vacias.");
                    }

                    if (!PasswordPolicy.IsStrong(dto.Password, out var pwdMsg))
                    {
                        return BadRequest(pwdMsg);
                    }

                    newPwd = dto.Password;
                }

                _usuarioRepository.Update(usuario, newPwd);
                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en actualizar-usuario {UsuarioId}", id);
                return StatusCode(500, "Error interno al actualizar usuario");
            }
        }

        [HttpDelete("eliminar-usuario/{id:int}")]
        public IActionResult Delete(int id)
        {
            try
            {
                _logger.LogInformation("DELETE eliminar-usuario {UsuarioId}", id);
                _usuarioRepository.Delete(id);
                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error en eliminar-usuario {UsuarioId}", id);
                return StatusCode(500, "Error interno al eliminar usuario");
            }
        }
    }
}
