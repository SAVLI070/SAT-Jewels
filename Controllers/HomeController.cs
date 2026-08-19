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
                // Query exact product count per numeric CategoryId directly from Neon PostgreSQL DB
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
