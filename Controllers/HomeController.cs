using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Guests see marketing landing; signed-in clients go straight to Products
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect("/admin");
                }

                return RedirectToAction("Index", "Product");
            }

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
