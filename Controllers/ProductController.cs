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
                ViewBag.Message = "Product identifier is required. Please select a piece from our live storefront collections.";
                return View("RestrictedAccess");
            }

            // 1. Query active catalog item by ID (Strict check, NO fallback to prevent ID enumeration)
            var product = await _context.CatalogItems.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (product == null)
            {
                ViewBag.Message = "You cannot access this product directly or it is currently unavailable in our catalog.";
                return View("RestrictedAccess");
            }

            // 2. Query parent category and verify it is ACTIVE
            // If the category is hidden/disabled by Admin, access to any item in that category via URL is strictly blocked!
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == product.CategoryId && c.IsActive);
            if (category == null)
            {
                ViewBag.Message = "This collection category is currently hidden by the curator and cannot be accessed directly.";
                return View("RestrictedAccess");
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
