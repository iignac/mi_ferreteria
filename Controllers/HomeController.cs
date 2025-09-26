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
            _logger.LogInformation("Cargando Home/Index");
            return View();
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error en Home/Index");
            return Problem("Ocurrió un error al cargar la página principal.");
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
            return Problem("Ocurrió un error al cargar la página de privacidad.");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
