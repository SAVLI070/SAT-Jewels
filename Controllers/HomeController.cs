using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
