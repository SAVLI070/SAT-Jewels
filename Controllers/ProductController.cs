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
        // OWASP A01: Strict Access Control & Hidden Category Guard
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Index", "Home");
            }

            // 1. Query active catalog item by ID (Strict check, NO fallback to prevent ID enumeration)
            var product = await _context.CatalogItems.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (product == null)
            {
                // OWASP A01 Protection: Return 404 NotFound if product ID is invalid or disabled
                return NotFound();
            }

            // 2. Query parent category and verify it is ACTIVE
            // If the category is hidden/disabled by Admin, access to any item in that category via URL is strictly blocked!
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == product.CategoryId && c.IsActive);
            if (category == null)
            {
                // Parent category is hidden or disabled by Admin -> Return 404 NotFound
                return NotFound();
            }

            ViewBag.CategoryName = category.Name;
            ViewBag.CategoryBadge = category.Badge;

            // 3. Related active items from the SAME active category only
            var relatedItems = await _context.CatalogItems
                .Where(i => i.CategoryId == product.CategoryId && i.Id != product.Id && i.IsActive)
                .OrderBy(i => i.CreatedAt)
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
