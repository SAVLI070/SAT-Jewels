using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    [AllowAnonymous]
    public class ProductController : Controller
    {
        private readonly SatJewelDbContext _context;

        public ProductController(SatJewelDbContext context)
        {
            _context = context;
        }

        // GET: /Product/Details/{id}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return RedirectToAction("Index", "Home");

            // Query Neon PostgreSQL DB for product
            var product = await _context.CatalogItems.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (product == null)
            {
                // Fallback search for first available item
                product = await _context.CatalogItems.FirstOrDefaultAsync();
            }

            if (product == null) return RedirectToAction("Index", "Home");

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == product.CategoryId);
            ViewBag.CategoryName = category?.Name ?? "Haute Joaillerie";
            ViewBag.CategoryBadge = category?.Badge ?? "GIA Certified";

            // Related items from same category
            var relatedItems = await _context.CatalogItems
                .Where(i => i.CategoryId == product.CategoryId && i.Id != product.Id && i.IsActive)
                .Take(3)
                .ToListAsync();

            ViewBag.RelatedItems = relatedItems;

            return View(product);
        }

        // GET: /Product/Cart
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Cart()
        {
            return View();
        }
    }
}
