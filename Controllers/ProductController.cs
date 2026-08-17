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
        private readonly BAL.CatalogBal _catalogBal;

        public ProductController(SatJewelDbContext context, IWebHostEnvironment env, BAL.CatalogBal catalogBal)
        {
            _context = context;
            _env = env;
            _catalogBal = catalogBal;
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

        // GET: /Product/Category?id=2 & /Product/Category?name=Rose+Cut
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Category(string? name, long? id, RingCategoryEnum? categoryEnum, string? shape, string? sort, string? diamondType)
        {
            List<CatalogItem> products;
            string categoryName = "Anniversary Ring";

            if (id.HasValue && id.Value > 0)
            {
                var catEnum = (RingCategoryEnum)id.Value;
                categoryName = catEnum.ToString();
                ViewBag.CategoryId = id.Value;
                products = await _catalogBal.GetProductsByNumericIdAsync(id.Value, _env.WebRootPath);
            }
            else if (categoryEnum.HasValue)
            {
                long catNum = (long)categoryEnum.Value;
                categoryName = categoryEnum.Value.ToString();
                ViewBag.CategoryId = catNum;
                products = await _catalogBal.GetProductsByEnumCategoryAsync(categoryEnum.Value, _env.WebRootPath);
            }
            else if (long.TryParse(name, out long parsedNumericId))
            {
                ViewBag.CategoryId = parsedNumericId;
                products = await _catalogBal.GetProductsByNumericIdAsync(parsedNumericId, _env.WebRootPath);
            }
            else
            {
                categoryName = string.IsNullOrWhiteSpace(name) ? "Anniversary Ring" : name;
                products = await _catalogBal.GetProductsByCategoryIdAsync(categoryName, _env.WebRootPath);
            }

            ViewBag.CategoryName = categoryName;
            ViewBag.SelectedShape = shape ?? "All";
            ViewBag.SelectedSort = sort ?? "bestselling";
            ViewBag.DiamondType = diamondType ?? "Lab Grown";

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

            CatalogItem? product = null;

            // 1. Try Database Query first
            try
            {
                product = await _context.CatalogItems.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            }
            catch { }

            // 2. If not found in Database, search LocalStore across all subcategories
            if (product == null)
            {
                var allFolders = new[]
                {
                    "lab_diamond_anniversary_ring", "antique_cut", "engagement_ring",
                    "eternity_ring", "fancy_color", "nature_inspired", "natural_rainbow",
                    "three_stone", "rose_cut", "marquise_shape", "halo_ring", "solitaire_ring"
                };

                foreach (var folder in allFolders)
                {
                    var localItems = BAL.LocalStore.GetLocalCategoryProducts(folder, _env.WebRootPath);
                    var match = localItems.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        product = match;
                        break;
                    }
                }
            }

            // 3. Robust fallback matching by category key if ID string contains category folder
            if (product == null)
            {
                var cleanId = id.ToLower();
                var allFolders = new[]
                {
                    "lab_diamond_anniversary_ring", "antique_cut", "engagement_ring",
                    "eternity_ring", "fancy_color", "nature_inspired", "natural_rainbow",
                    "three_stone", "rose_cut", "marquise_shape", "halo_ring", "solitaire_ring"
                };

                foreach (var folder in allFolders)
                {
                    if (cleanId.Contains(folder))
                    {
                        var localItems = BAL.LocalStore.GetLocalCategoryProducts(folder, _env.WebRootPath);
                        if (localItems.Count > 0)
                        {
                            product = localItems[0];
                            break;
                        }
                    }
                }
            }

            // 4. Default fallback product so user page NEVER breaks
            if (product == null)
            {
                var defaultItems = BAL.LocalStore.GetLocalCategoryProducts("anniversary ring", _env.WebRootPath);
                product = defaultItems.FirstOrDefault() ?? new CatalogItem
                {
                    Id = id,
                    Name = "Exquisite Custom Diamond Ring",
                    CategoryId = "lab_diamond_anniversary_ring",
                    Spec = "18K Gold | 1.5ct GIA VVS1 | Brilliant Cut",
                    PriceUSD = 2400,
                    ImageUrl = "/assets/ivevar/exclusive_regal_star_diamond_ring.jpg",
                    GalleryImages = "/assets/ivevar/exclusive_regal_star_diamond_ring.jpg",
                    IsActive = true
                };
            }

            // 5. Category Metadata & Related Product Recommendations
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == product.CategoryId && c.IsActive);
            ViewBag.CategoryName = category?.Name ?? System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(product.CategoryId.Replace("_", " "));
            ViewBag.CategoryBadge = category?.Badge ?? "GIA Certified";

            var relatedItems = BAL.LocalStore.GetLocalCategoryProducts(product.CategoryId, _env.WebRootPath)
                .Where(i => i.Id != product.Id)
                .Take(4)
                .ToList();

            if (relatedItems.Count == 0)
            {
                relatedItems = BAL.LocalStore.GetLocalCategoryProducts("anniversary ring", _env.WebRootPath)
                    .Take(4)
                    .ToList();
            }

            ViewBag.RelatedItems = relatedItems;

            return View(product);
        }
    }
}
