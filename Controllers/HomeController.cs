using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly SatJewelDbContext _context;

        public HomeController(IWebHostEnvironment env, SatJewelDbContext context)
        {
            _env = env;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var dbCounts = new Dictionary<long, int>();
            try
            {
                dbCounts = await _context.Products
                    .Where(p => p.IsAvailable)
                    .GroupBy(p => p.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
            }
            catch
            {
                dbCounts = new Dictionary<long, int>();
            }

            ViewBag.CategoryCountsByNumericId = dbCounts;
            ViewData["Title"] = "SAT Jewel — Fine Jewelry | Mastery in Every Cut";
            return View("LandingNew");
        }

        [HttpGet]
        public async Task<IActionResult> LandingNew()
        {
            var dbCounts = new Dictionary<long, int>();
            try
            {
                dbCounts = await _context.Products
                    .Where(p => p.IsAvailable)
                    .GroupBy(p => p.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
            }
            catch
            {
                dbCounts = new Dictionary<long, int>();
            }

            ViewBag.CategoryCountsByNumericId = dbCounts;
            ViewData["Title"] = "SAT Jewel — Fine Jewelry | Mastery in Every Cut";
            return View();
        }

        [HttpGet]
        public IActionResult About()
        {
            ViewData["Title"] = "About Us & Our Story — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult Faq()
        {
            ViewData["Title"] = "Frequently Asked Questions — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult PaymentPolicy()
        {
            ViewData["Title"] = "Payment Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult PrivacyPolicy()
        {
            ViewData["Title"] = "Privacy Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult RefundPolicy()
        {
            ViewData["Title"] = "Refund & Return Policy — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult CustomRings()
        {
            ViewData["Title"] = "Design Your Own Custom Engagement Ring — SAT Jewel Sanctuary";
            return View();
        }

        [HttpGet]
        public IActionResult CraftProcess()
        {
            return RedirectToAction("CustomRings");
        }

        [HttpGet]
        public IActionResult Blog()
        {
            ViewData["Title"] = "Jewelry Education & Lab Diamond Insights — SAT Jewel Blog";
            return View();
        }

        [HttpGet]
        public IActionResult RingSizeGuide()
        {
            ViewData["Title"] = "Find Your Ring Size Guide — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult JewelryCare()
        {
            ViewData["Title"] = "Fine Jewelry Care & Cleaning Guide — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult OrderProcess()
        {
            ViewData["Title"] = "Custom Order & Crafting Process — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult DiamondSizeChart()
        {
            ViewData["Title"] = "Carat Weight & Diamond Size Chart — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult MoissaniteVsDiamondSizeChart()
        {
            ViewData["Title"] = "Moissanite vs Diamond Size Chart — SAT Jewel";
            return View();
        }

        [HttpGet]
        public IActionResult DiamondComparisonGuide()
        {
            ViewData["Title"] = "Moissanite vs Lab Grown Diamond vs Mined Diamond — SAT Jewel";
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
