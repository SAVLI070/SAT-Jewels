using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IActionResult Index()
        {
            ViewBag.CategoryCounts = BAL.LocalStore.GetCategoryCounts(_env.WebRootPath);
            return View();
        }

        [HttpGet]
        public IActionResult Restricted()
        {
            ViewBag.Message = "The page or item you requested is not accessible directly or has been moved.";
            return View("~/Views/Shared/RestrictedAccess.cshtml");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("~/Views/Shared/RestrictedAccess.cshtml");
        }
    }
}
