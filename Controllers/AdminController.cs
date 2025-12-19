using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using mi_ferreteria.Data;
using mi_ferreteria.ViewModels;

namespace mi_ferreteria.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private readonly IVentaRepository _ventaRepo;
        private readonly IStockRepository _stockRepo;
        private readonly IProductoRepository _productoRepo;
        private readonly IUsuarioRepository _usuarioRepo;
        private readonly IAuditoriaRepository _auditoriaRepo;
        private readonly IReporteFinancieroRepository _finanzasRepo;
        private readonly ICategoriaRepository _categoriaRepo;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IVentaRepository ventaRepo,
            IStockRepository stockRepo,
            IProductoRepository productoRepo,
            IUsuarioRepository usuarioRepo,
            IAuditoriaRepository auditoriaRepo,
            IReporteFinancieroRepository finanzasRepo,
            ICategoriaRepository categoriaRepo,
            ILogger<AdminController> logger)
        {
            _ventaRepo = ventaRepo;
            _stockRepo = stockRepo;
            _productoRepo = productoRepo;
            _usuarioRepo = usuarioRepo;
            _auditoriaRepo = auditoriaRepo;
            _finanzasRepo = finanzasRepo;
            _categoriaRepo = categoriaRepo;
            _logger = logger;
        }

        public IActionResult Dashboard()
        {
            try
            {
                const int topDashboardRows = 5;
                var model = new AdminDashboardViewModel
                {
                    TotalVentas = _ventaRepo.CountAll(),
                    TotalMovimientosStock = _stockRepo.CountMovimientosGlobal(null),
                    TotalMovimientosIngreso = _stockRepo.CountMovimientosGlobal("INGRESO"),
                    TotalMovimientosEgreso = _stockRepo.CountMovimientosGlobal("EGRESO"),
                    TotalProductos = _productoRepo.CountAll(),
                    ProductosInactivos = _productoRepo.CountInactive(),
                    UltimasVentas = _ventaRepo.GetPage(1, topDashboardRows).ToList(),
                    UltimosMovimientosStock = _stockRepo.GetUltimosMovimientos(null, topDashboardRows).ToList(),
                    UltimasAltas = _productoRepo.GetLastCreated(topDashboardRows).ToList(),
                    UltimasBajas = _productoRepo.GetLastInactive(topDashboardRows).ToList(),
                    UltimasEdiciones = _productoRepo.GetLastUpdated(topDashboardRows).ToList()
                };
                var criticosDestacados = _stockRepo.GetProductosStockCritico(null, 1, topDashboardRows, out var totalCriticos).ToList();
                model.ProductosStockCritico = totalCriticos;
                model.ProductosCriticosDestacados = criticosDestacados;

                var productosMapa = new Dictionary<long, string>();
                foreach (var pid in model.UltimosMovimientosStock.Select(m => m.ProductoId).Distinct())
                {
                    var prod = _productoRepo.GetById(pid);
                    if (prod != null)
                    {
                        productosMapa[pid] = prod.Nombre;
                    }
                }
                model.ProductosPorId = productosMapa;

                var usuarios = _usuarioRepo.GetAll();
                model.AdministradoresActivos = usuarios
                    .Where(u => u.Activo && u.Roles.Any(r => string.Equals(r.Nombre, "Administrador", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var alertas = new List<string>();
                if (model.AdministradoresActivos.Count < 2)
                {
                    alertas.Add("Hay menos de 2 administradores activos; considera agregar otro para redundancia y control cruzado.");
                }
                if (model.ProductosInactivos > 0)
                {
                    alertas.Add($"Existen {model.ProductosInactivos} productos inactivos; revisa que correspondan a bajas legitimas.");
                }
                if (model.TotalMovimientosEgreso > model.TotalMovimientosIngreso * 2 && model.TotalMovimientosEgreso > 10)
                {
                    alertas.Add("Los egresos de stock superan ampliamente a los ingresos recientes. Valida posibles faltantes o ajustes no autorizados.");
                }
                model.AlertasSeguridad = alertas;

                ViewData["Title"] = "Panel de administracion";
                return View("Dashboard", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo construir el panel de administracion");
                return Problem("No se pudo cargar el panel de administracion.");
            }
        }

        public IActionResult Auditoria(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                if (page < 1) page = 1;
                var (registros, total) = _auditoriaRepo.GetPage(page, pageSize);
                var totalPages = (int)Math.Ceiling(total / (double)pageSize);
                if (totalPages == 0) totalPages = 1;
                if (page > totalPages)
                {
                    page = totalPages;
                    (registros, total) = _auditoriaRepo.GetPage(page, pageSize);
                }

                var model = new AuditoriaListadoViewModel
                {
                    Registros = registros.ToList(),
                    Page = page,
                    TotalPages = totalPages,
                    TotalCount = total
                };

                ViewData["Title"] = "Auditoria de usuarios";
                return View("Auditoria", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo cargar la auditoria");
                return Problem("No se pudo cargar la auditoria.");
            }
        }

        public IActionResult Finanzas(int diasTopProductos = 30, int diasTopClientes = 30, long? categoriaTopProductosId = null)
        {
            try
            {
                if (diasTopProductos != 30 && diasTopProductos != 90)
                {
                    diasTopProductos = 30;
                }
                if (diasTopClientes != 30 && diasTopClientes != 90)
                {
                    diasTopClientes = 30;
                }

                if (categoriaTopProductosId.HasValue && categoriaTopProductosId.Value <= 0)
                {
                    categoriaTopProductosId = null;
                }

                var categorias = _categoriaRepo
                    .GetAll()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .ToList();

                if (categoriaTopProductosId.HasValue && categorias.All(c => c.Id != categoriaTopProductosId.Value))
                {
                    categoriaTopProductosId = null;
                }

                var resumen = _finanzasRepo.ObtenerResumen(diasTopProductos, diasTopClientes, categoriaTopProductosId);
                var vm = new FinanzasDashboardViewModel
                {
                    Resumen = resumen,
                    DiasTopProductos = diasTopProductos,
                    DiasTopClientes = diasTopClientes,
                    CategoriaTopProductosId = categoriaTopProductosId,
                    Categorias = categorias
                };
                ViewData["Title"] = "Tablero financiero";
                return View("Finanzas", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo cargar el tablero financiero");
                return Problem("No se pudo cargar el tablero financiero.");
            }
        }
    }
}
