using Microsoft.AspNetCore.Mvc;

namespace mi_ferreteria.Controllers
{
    public class StockController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
