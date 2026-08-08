using Microsoft.AspNetCore.Mvc;

namespace SAT1.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewBag.Title = "Dashboard Overview";
            return View();
        }

        public IActionResult Catalog()
        {
            ViewBag.Title = "Live Jewelry Catalog Table";
            return View();
        }

        public IActionResult AddProduct()
        {
            ViewBag.Title = "Publish New Collection Item";
            return View();
        }
    }
}
