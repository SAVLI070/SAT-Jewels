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

        // GET: /Product — dedicated shop page (logged-in clients only)
        [HttpGet]
        public async Task<IActionResult> Index(string? category = null)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                // Not logged in → marketing landing
                return RedirectToAction("Index", "Home");
            }

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var productsQuery = _context.CatalogItems.Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(category))
            {
                var catExists = categories.Any(c => c.Id == category);
                if (catExists)
                {
                    productsQuery = productsQuery.Where(p => p.CategoryId == category);
                }
                else
                {
                    category = null;
                }
            }

            var products = await productsQuery
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.ActiveCategory = category ?? "all";
            ViewBag.ProductCount = products.Count;

            return View(products);
        }

        // GET: /Product/Details/{id}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                ViewBag.Message = "Product identifier is required. Please select a piece from our live storefront collections.";
                return View("RestrictedAccess");
            }

            var product = await _context.CatalogItems.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (product == null)
            {
                ViewBag.Message = "You cannot access this product directly or it is currently unavailable in our catalog.";
                return View("RestrictedAccess");
            }

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == product.CategoryId && c.IsActive);
            if (category == null)
            {
                ViewBag.Message = "This collection category is currently hidden by the curator and cannot be accessed directly.";
                return View("RestrictedAccess");
            }

            ViewBag.CategoryName = category.Name;
            ViewBag.CategoryBadge = category.Badge;

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
