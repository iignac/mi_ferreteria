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
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IVentaRepository ventaRepo,
            IStockRepository stockRepo,
            IProductoRepository productoRepo,
            IUsuarioRepository usuarioRepo,
            ILogger<AdminController> logger)
        {
            _ventaRepo = ventaRepo;
            _stockRepo = stockRepo;
            _productoRepo = productoRepo;
            _usuarioRepo = usuarioRepo;
            _logger = logger;
        }

        public IActionResult Dashboard()
        {
            try
            {
                var model = new AdminDashboardViewModel
                {
                    TotalVentas = _ventaRepo.CountAll(),
                    TotalMovimientosStock = _stockRepo.CountMovimientosGlobal(null),
                    TotalMovimientosIngreso = _stockRepo.CountMovimientosGlobal("INGRESO"),
                    TotalMovimientosEgreso = _stockRepo.CountMovimientosGlobal("EGRESO"),
                    TotalProductos = _productoRepo.CountAll(),
                    ProductosInactivos = _productoRepo.CountInactive(),
                    UltimasVentas = _ventaRepo.GetPage(1, 5).ToList(),
                    UltimosMovimientosStock = _stockRepo.GetUltimosMovimientos(null, 8).ToList(),
                    UltimasAltas = _productoRepo.GetLastCreated(5).ToList(),
                    UltimasBajas = _productoRepo.GetLastInactive(5).ToList(),
                    UltimasEdiciones = _productoRepo.GetLastUpdated(5).ToList()
                };

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
    }
}
