using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        [HttpGet("")]
        [HttpGet("index")]
        [HttpGet("dashboard")]
        public IActionResult Index()
        {
            ViewBag.Title = "Dashboard Overview";
            return View("Index");
        }

        [HttpGet("categories")]
        public IActionResult Categories()
        {
            ViewBag.Title = "Jewelry Category Management";
            return View();
        }

        [HttpGet("catalog")]
        public IActionResult Catalog()
        {
            ViewBag.Title = "Live Jewelry Catalog Table";
            return View();
        }

        [HttpGet("addproduct")]
        public IActionResult AddProduct()
        {
            ViewBag.Title = "Publish New Collection Item";
            return View();
        }
    }
}
