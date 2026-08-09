using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private bool CheckAdminAccess()
        {
            if (User.Identity?.IsAuthenticated != true) return false;
            
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value?.ToLower() ?? "";
            
            if (userRole == "Admin" || userEmail.Contains("admin") || User.Identity.Name == "SAT Administrator")
            {
                return true;
            }
            
            return false;
        }

        [HttpGet("")]
        [HttpGet("index")]
        [HttpGet("dashboard")]
        public IActionResult Index()
        {
            if (!CheckAdminAccess())
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/admin", adminRequired = "true" });
            }
            ViewBag.Title = "Dashboard Overview";
            return View("Index");
        }

        [HttpGet("categories")]
        public IActionResult Categories()
        {
            if (!CheckAdminAccess())
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/admin/categories", adminRequired = "true" });
            }
            ViewBag.Title = "Jewelry Category Management";
            return View();
        }

        [HttpGet("catalog")]
        public IActionResult Catalog()
        {
            if (!CheckAdminAccess())
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/admin/catalog", adminRequired = "true" });
            }
            ViewBag.Title = "Live Jewelry Catalog Table";
            return View();
        }

        [HttpGet("addproduct")]
        public IActionResult AddProduct()
        {
            if (!CheckAdminAccess())
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/admin/addproduct", adminRequired = "true" });
            }
            ViewBag.Title = "Publish New Collection Item";
            return View();
        }
    }
}
