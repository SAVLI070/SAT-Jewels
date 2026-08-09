using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;

namespace SAT1.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly AdminBal _adminBal;

        public AdminController(AdminBal adminBal)
        {
            _adminBal = adminBal;
        }

        private bool CheckAccess()
        {
            return _adminBal.CheckAdminAccess(User);
        }

        [HttpGet("")]
        [HttpGet("index")]
        [HttpGet("dashboard")]
        public IActionResult Index()
        {
            if (!CheckAccess())
            {
                return Redirect("/Account/SignIn");
            }
            ViewBag.Title = "Dashboard Overview";
            return View("Index");
        }

        [HttpGet("categories")]
        public IActionResult Categories()
        {
            if (!CheckAccess())
            {
                return Redirect("/Account/SignIn");
            }
            ViewBag.Title = "Jewelry Category Management";
            return View();
        }

        [HttpGet("catalog")]
        public IActionResult Catalog()
        {
            if (!CheckAccess())
            {
                return Redirect("/Account/SignIn");
            }
            ViewBag.Title = "Live Jewelry Catalog Table";
            return View();
        }

        [HttpGet("addproduct")]
        public IActionResult AddProduct()
        {
            if (!CheckAccess())
            {
                return Redirect("/Account/SignIn");
            }
            ViewBag.Title = "Publish New Collection Item";
            return View();
        }
    }
}
