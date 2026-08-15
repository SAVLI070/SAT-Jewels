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
        private readonly IWebHostEnvironment _env;

        public ProductController(SatJewelDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: /Product (Shop Catalog)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var products = await _context.CatalogItems
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            if (products.Count == 0)
            {
                products = BAL.LocalStore.GetLocalCategoryProducts("anniversary ring", _env.WebRootPath);
            }
            return View(products);
        }

        // GET: /Product/Cart (Shopping Cart)
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Cart()
        {
            return View();
        }

        // GET: /Product/Category?name=Rose+Cut&shape=Cushion&sort=bestselling
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Category(string? name, string? shape, string? sort, string? diamondType)
        {
            var categoryName = string.IsNullOrWhiteSpace(name) ? "Anniversary Ring" : name;
            ViewBag.CategoryName = categoryName;
            ViewBag.SelectedShape = shape ?? "All";
            ViewBag.SelectedSort = sort ?? "bestselling";
            ViewBag.DiamondType = diamondType ?? "Lab Grown";

            // Load authentic local ring product images dynamically from wwwroot/assets/ivevar/
            var products = BAL.LocalStore.GetLocalCategoryProducts(categoryName, _env.WebRootPath);

            // Filter by Shape if specified
            if (!string.IsNullOrWhiteSpace(shape) && shape.ToLower() != "all")
            {
                var shapeTerm = shape.ToLower();
                var filtered = products.Where(p => p.Name.ToLower().Contains(shapeTerm) || p.Spec.ToLower().Contains(shapeTerm) || p.ImageUrl.ToLower().Contains(shapeTerm)).ToList();
                if (filtered.Count > 0) products = filtered;
            }

            // Apply Sorting
            switch (sort?.ToLower())
            {
                case "price-asc":
                    products = products.OrderBy(p => p.PriceUSD).ToList();
                    break;
                case "price-desc":
                    products = products.OrderByDescending(p => p.PriceUSD).ToList();
                    break;
                case "alpha-asc":
                    products = products.OrderBy(p => p.Name).ToList();
                    break;
                default:
                    products = products.OrderByDescending(p => p.CreatedAt).ToList();
                    break;
            }

            return View("Category", products);
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
            ViewBag.CategoryName = category?.Name ?? (!string.IsNullOrWhiteSpace(product.CategoryId) ? product.CategoryId : "Fine Jewelry");
            ViewBag.CategoryBadge = category?.Badge ?? "GIA Certified";

            var relatedItems = await _context.CatalogItems
                .Where(i => i.Id != product.Id && i.IsActive)
                .OrderBy(i => i.CreatedAt)
                .Take(3)
                .ToListAsync();

            ViewBag.RelatedItems = relatedItems;

            return View(product);
        }
    }
}
