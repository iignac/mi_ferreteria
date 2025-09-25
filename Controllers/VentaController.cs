using Microsoft.AspNetCore.Mvc;

namespace mi_ferreteria.Controllers
{
    public class VentaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
