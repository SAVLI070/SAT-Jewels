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

        // GET: /Product/Category?id=2 or /Product/Category/2 or /Product/Category/anniversary-ring
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Category(string? id, long? catId, RingCategoryEnum? categoryEnum, string? name, string? shape, string? sort, string? diamondType, int page = 1, int pageSize = 12)
        {
            long categoryId = 2; // Default to AnniversaryRings (Id = 2)

            if (!string.IsNullOrWhiteSpace(id))
            {
                if (long.TryParse(id, out long parsedFromId))
                {
                    categoryId = parsedFromId;
                }
                else
                {
                    // Attempt slug matching (e.g. "anniversary-ring", "engagement-ring")
                    var cleanSlug = id.Replace("-", "").Replace("_", "").ToLower();
                    foreach (RingCategoryEnum e in Enum.GetValues(typeof(RingCategoryEnum)))
                    {
                        if (e.ToString().ToLower().Replace("-", "").Replace("_", "") == cleanSlug)
                        {
                            categoryId = (long)e;
                            break;
                        }
                    }
                }
            }
            else if (catId.HasValue && catId.Value > 0)
            {
                categoryId = catId.Value;
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

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            var pagedResult = await _catalogBal.GetCategoryProductsPagedAsync(categoryId, page, pageSize, shape, sort, _env.WebRootPath);

            ViewBag.SelectedShape = shape ?? "All";
            ViewBag.SelectedSort = sort ?? SortOptionEnum.Bestselling.ToString().ToLower();
            ViewBag.DiamondType = diamondType ?? DiamondTypeEnum.LabGrown.GetDisplayName();
            ViewBag.CurrentPage = pagedResult.Page;
            ViewBag.PageSize = pagedResult.PageSize;
            ViewBag.TotalCount = pagedResult.TotalCount;
            ViewBag.TotalPages = pagedResult.TotalPages;
            ViewBag.HasPreviousPage = pagedResult.HasPreviousPage;
            ViewBag.HasNextPage = pagedResult.HasNextPage;

            return View("Category", pagedResult.Items);
        }

        // GET: /Product/Details/{id}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(string? id, string? itemid, string? productId)
        {
            var targetId = id ?? itemid ?? productId;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                targetId = "sat-prod-8f3a9b2c1d4e";
            }

            // MAIN RULE: Data retrieval strictly by primary key ID / numeric ProductId
            CatalogItem? product = await _catalogBal.GetCatalogItemByIdAsync(targetId);

            // Default fallback product so user page NEVER breaks or redirects to RestrictedAccess
            if (product == null)
            {
                var defaultItems = await _catalogBal.GetProductsByNumericIdAsync(2, _env.WebRootPath);
                product = defaultItems.FirstOrDefault() ?? new CatalogItem
                {
                    Id = targetId,
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

            var catIdToQuery = long.TryParse(product.CategoryId, out long cId) ? cId : 1;
            var relatedItems = (await _catalogBal.GetProductsByCategoryAndShapeAsync(catIdToQuery, null, _env.WebRootPath))
                .Where(i => i.Id != product.Id)
                .Take(12)
                .ToList();

            ViewBag.RelatedItems = relatedItems;

            var reviewBal = HttpContext.RequestServices.GetService<BAL.ReviewBal>();
            if (reviewBal != null)
            {
                ViewBag.PhotoReviews = await reviewBal.GetStorefrontPhotoReviewsAsync(product.Id.ToString());
            }

            return View(product);
        }
    }
}
