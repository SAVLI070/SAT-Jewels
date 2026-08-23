using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;

namespace SAT1.Controllers
{
    [Route("admin")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

        private IActionResult HandleUnauthorized()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.Message = $"You are currently signed in as customer '{User.Identity.Name}'. The Admin Portal requires Administrator privileges. Please sign out and log in with your Admin credentials.";
                return View("~/Views/Shared/RestrictedAccess.cshtml");
            }

            return Redirect("/Account/SignIn?returnUrl=" + System.Net.WebUtility.UrlEncode(Request.Path));
        }

        [HttpGet("")]
        [HttpGet("index")]
        [HttpGet("dashboard")]
        public IActionResult Index()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Dashboard Overview";
            return View("Index");
        }

        [HttpGet("categories")]
        public IActionResult Categories()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Jewelry Category Management";
            return View();
        }

        [HttpGet("catalog")]
        public IActionResult Catalog()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Live Jewelry Catalog Table";
            return View();
        }

        [HttpGet("addproduct")]
        public IActionResult AddProduct()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Publish New Collection Item";
            return View();
        }
    }
}
