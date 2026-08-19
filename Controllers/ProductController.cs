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
        public async Task<IActionResult> Cart()
        {
            ViewBag.SavedAddresses = new List<UserAddress>();
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    try
                    {
                        ViewBag.SavedAddresses = await _context.UserAddresses
                            .Where(a => a.UserId == userId)
                            .OrderByDescending(a => a.IsDefault)
                            .ToListAsync();
                    }
                    catch
                    {
                        ViewBag.SavedAddresses = new List<UserAddress>();
                    }
                }
            }
            return View();
        }

        // GET: /Product/Category?id=2
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Category(long? id, RingCategoryEnum? categoryEnum, string? name, string? shape, string? sort, string? diamondType)
        {
            // MAIN RULE: Data retrieval is strictly based on numeric CategoryId / RingCategoryEnum ID
            long categoryId = 2; // Default to AnniversaryRings (Id = 2)

            if (id.HasValue && id.Value > 0)
            {
                categoryId = id.Value;
            }
            else if (categoryEnum.HasValue)
            {
                categoryId = (long)categoryEnum.Value;
            }
            else if (!string.IsNullOrWhiteSpace(name) && long.TryParse(name, out long parsedId))
            {
                categoryId = parsedId;
            }

            var enumVal = Enum.IsDefined(typeof(RingCategoryEnum), categoryId)
                ? (RingCategoryEnum)categoryId
                : RingCategoryEnum.AnniversaryRings;

            string categoryDisplayName = enumVal.GetDisplayName();
            ViewBag.CategoryId = categoryId;
            ViewBag.CategoryEnum = enumVal;
            ViewBag.CategoryName = categoryDisplayName;

            // Fetch products strictly by numeric long CategoryId
            List<CatalogItem> products = await _catalogBal.GetProductsByNumericIdAsync(categoryId, _env.WebRootPath);

            ViewBag.SelectedShape = shape ?? "All";
            ViewBag.SelectedSort = sort ?? SortOptionEnum.Bestselling.ToString().ToLower();
            ViewBag.DiamondType = diamondType ?? DiamondTypeEnum.LabGrown.GetDisplayName();

            // Filter by Shape if specified
            if (!string.IsNullOrWhiteSpace(shape) && shape.ToLower() != "all")
            {
                var shapeTerm = shape.ToLower();
                var filtered = products.Where(p => p.Name.ToLower().Contains(shapeTerm) || p.Spec.ToLower().Contains(shapeTerm) || p.ImageUrl.ToLower().Contains(shapeTerm)).ToList();
                if (filtered.Count > 0) products = filtered;
            }

            // Apply Enum-based Sorting
            switch (sort?.ToLower())
            {
                case "price-asc":
                case "priceasc":
                    products = products.OrderBy(p => p.PriceUSD).ToList();
                    break;
                case "price-desc":
                case "pricedesc":
                    products = products.OrderByDescending(p => p.PriceUSD).ToList();
                    break;
                case "alpha-asc":
                case "alphaasc":
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

            // MAIN RULE: Data retrieval strictly by primary key ID / numeric ProductId
            CatalogItem? product = await _catalogBal.GetCatalogItemByIdAsync(id);

            // Default fallback product so user page NEVER breaks
            if (product == null)
            {
                var defaultItems = await _catalogBal.GetProductsByNumericIdAsync(2, _env.WebRootPath);
                product = defaultItems.FirstOrDefault() ?? new CatalogItem
                {
                    Id = id,
                    Name = "Exquisite Custom Diamond Ring",
                    CategoryId = "2",
                    Spec = "18K Gold | 1.5ct GIA VVS1 | Brilliant Cut",
                    PriceUSD = 2400,
                    ImageUrl = "/assets/ivevar/exclusive_regal_star_diamond_ring.jpg",
                    GalleryImages = "/assets/ivevar/exclusive_regal_star_diamond_ring.jpg",
                    IsActive = true
                };
            }

            // Category Metadata & Related Product Recommendations using numeric CategoryId
            if (long.TryParse(product.CategoryId, out long catId) && Enum.IsDefined(typeof(RingCategoryEnum), catId))
            {
                ViewBag.CategoryName = ((RingCategoryEnum)catId).GetDisplayName();
            }
            else
            {
                ViewBag.CategoryName = "Fine Jewelry";
            }
            ViewBag.CategoryBadge = "GIA Certified";

            var relatedItems = (await _catalogBal.GetProductsByNumericIdAsync(long.TryParse(product.CategoryId, out long cId) ? cId : 2, _env.WebRootPath))
                .Where(i => i.Id != product.Id)
                .Take(4)
                .ToList();

            ViewBag.RelatedItems = relatedItems;

            return View(product);
        }
    }
}
