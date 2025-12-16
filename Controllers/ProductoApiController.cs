using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.Models;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System;

namespace mi_ferreteria.Controllers
{
    [ApiController]
    [Route("api/productos")]
    public class ProductoApiController : ControllerBase
    {
        private readonly IProductoRepository _repo;
        private readonly ILogger<ProductoApiController> _logger;
        private static readonly string[] UnidadesPermitidas = new[] {
            "unidad","gramos","kilos","metros cuadrados","juego","bolsa","placa","rollo","litro","mililitro","bidon","kit","par"
        };

        public ProductoApiController(IProductoRepository repo, ILogger<ProductoApiController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet("listar-productos")]
        public ActionResult<IEnumerable<Producto>> Listar()
        {
            try
            {
                var items = _repo.GetAll();
                return Ok(items);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al listar productos");
                return StatusCode(500, "Error interno al listar productos");
            }
        }

        [HttpGet("obtener-producto/{id:long}")]
        public ActionResult<Producto> Obtener(long id)
        {
            try
            {
                var prod = _repo.GetById(id);
                if (prod == null) return NotFound();
                return Ok(prod);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al obtener producto {ProductoId}", id);
                return StatusCode(500, "Error interno al obtener producto");
            }
        }

        public class ProductoCreateDto
        {
            [Required] public string Sku { get; set; }
            [Required] public string Nombre { get; set; }
            public string? Descripcion { get; set; }
            public long? CategoriaId { get; set; }
            [Range(0, double.MaxValue)] public decimal PrecioVentaActual { get; set; }
            [Range(0, int.MaxValue)] public int StockMinimo { get; set; }
            public bool Activo { get; set; } = true;
            public long? UbicacionPreferidaId { get; set; }
            [Required] public string UnidadMedida { get; set; } = "unidad";
        }

        [HttpPost("crear-producto")]
        [Authorize(Roles = "Administrador,Stock")]
        public ActionResult<Producto> Crear([FromBody] ProductoCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return ValidationProblem(ModelState);
                if (_repo.SkuExists(dto.Sku))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        { "Sku", new[] { "El SKU ya existe" } }
                    }));
                }
                var unidad = (dto.UnidadMedida ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(unidad) || !UnidadesPermitidas.Contains(unidad))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        { "UnidadMedida", new[] { "Unidad de medida invalida." } }
                    }));
                }
                var p = new Producto
                {
                    Sku = dto.Sku,
                    Nombre = dto.Nombre,
                    Descripcion = dto.Descripcion,
                    CategoriaId = dto.CategoriaId,
                    PrecioVentaActual = dto.PrecioVentaActual,
                    StockMinimo = dto.StockMinimo,
                    UnidadMedida = unidad,
                    Activo = dto.Activo,
                    UbicacionPreferidaId = dto.UbicacionPreferidaId
                };
                _repo.Add(p);
                return Created($"/api/productos/obtener-producto/{p.Id}", p);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto {@Dto}", dto);
                return StatusCode(500, "Error interno al crear producto");
            }
        }

        public class ProductoUpdateDto : ProductoCreateDto
        {
            [Required] public long Id { get; set; }
        }

        [HttpPut("actualizar-producto/{id:long}")]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Actualizar(long id, [FromBody] ProductoUpdateDto dto)
        {
            try
            {
                if (id != dto.Id) return BadRequest("El ID de la ruta no coincide con el del cuerpo.");
                if (!ModelState.IsValid) return ValidationProblem(ModelState);
                if (_repo.SkuExists(dto.Sku, dto.Id))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        { "Sku", new[] { "El SKU ya existe en otro producto" } }
                    }));
                }
                var unidad = (dto.UnidadMedida ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(unidad) || !UnidadesPermitidas.Contains(unidad))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        { "UnidadMedida", new[] { "Unidad de medida invalida." } }
                    }));
                }
                var existente = _repo.GetById(dto.Id);
                if (existente == null) return NotFound();
                existente.Sku = dto.Sku;
                existente.Nombre = dto.Nombre;
                existente.Descripcion = dto.Descripcion;
                existente.CategoriaId = dto.CategoriaId;
                existente.PrecioVentaActual = dto.PrecioVentaActual;
                existente.StockMinimo = dto.StockMinimo;
                existente.Activo = dto.Activo;
                existente.UbicacionPreferidaId = dto.UbicacionPreferidaId;
                existente.UnidadMedida = unidad;
                _repo.Update(existente);
                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar producto {ProductoId}", id);
                return StatusCode(500, "Error interno al actualizar producto");
            }
        }

        [HttpDelete("eliminar-producto/{id:long}")]
        [Authorize(Roles = "Administrador,Stock")]
        public IActionResult Eliminar(long id)
        {
            try
            {
                _repo.Delete(id);
                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar producto {ProductoId}", id);
                return StatusCode(500, "Error interno al eliminar producto");
            }
        }
    }
}
