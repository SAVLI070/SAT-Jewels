using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Storefront Landing Page is accessible to all users (Guests, Clients, and Admins)
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
