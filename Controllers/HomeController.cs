using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using mi_ferreteria.Models;

namespace mi_ferreteria.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        try
        {
            _logger.LogInformation("Redirigiendo desde Home/Index");

            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Administrador")) return RedirectToAction("Dashboard", "Admin");
                if (User.IsInRole("Vendedor")) return RedirectToAction("Index", "Venta");
                if (User.IsInRole("Stock")) return RedirectToAction("Index", "Stock");
            }

            return RedirectToAction("Login", "Auth");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error en Home/Index");
            return Problem("Ocurrio un error al cargar la pagina principal.");
        }
    }

    public IActionResult Privacy()
    {
        try
        {
            _logger.LogInformation("Cargando Home/Privacy");
            return View();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error en Home/Privacy");
            return Problem("Ocurrio un error al cargar la pagina de privacidad.");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

