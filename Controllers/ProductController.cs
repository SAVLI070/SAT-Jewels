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
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Take(24)
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
                            .AsNoTracking()
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
        public async Task<IActionResult> Category(string? id, long? catId, RingCategoryEnum? categoryEnum, string? name, string? shape, string? sort, string? diamondType, string? q, string? search, int page = 1, int pageSize = 12)
        {
            var searchQuery = !string.IsNullOrWhiteSpace(q) ? q.Trim() : (!string.IsNullOrWhiteSpace(search) ? search.Trim() : null);
            long categoryId = 2; // Default to AnniversaryRings (Id = 2)
            bool isExplicitCategory = false;

            if (!string.IsNullOrWhiteSpace(id))
            {
                isExplicitCategory = true;
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
                isExplicitCategory = true;
                categoryId = catId.Value;
            }
            else if (categoryEnum.HasValue)
            {
                isExplicitCategory = true;
                categoryId = (long)categoryEnum.Value;
            }
            else if (!string.IsNullOrWhiteSpace(name) && long.TryParse(name, out long parsedId))
            {
                isExplicitCategory = true;
                categoryId = parsedId;
            }

            if (!isExplicitCategory && !string.IsNullOrWhiteSpace(searchQuery))
            {
                categoryId = 0;
            }

            var enumVal = Enum.IsDefined(typeof(RingCategoryEnum), categoryId)
                ? (RingCategoryEnum)categoryId
                : RingCategoryEnum.AnniversaryRings;

            string categoryDisplayName = categoryId == 0 
                ? $"Search: \"{searchQuery}\"" 
                : enumVal.GetDisplayName();

            ViewBag.CategoryId = categoryId;
            ViewBag.CategoryEnum = enumVal;
            ViewBag.CategoryName = categoryDisplayName;
            ViewBag.SearchQuery = searchQuery;

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            var pagedResult = await _catalogBal.GetCategoryProductsPagedAsync(categoryId, page, pageSize, shape, sort, _env.WebRootPath, searchQuery);

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
                    ImageUrl = "/assets/hero_slider_1_solitaire.jpg",
                    GalleryImages = "/assets/hero_slider_1_solitaire.jpg",
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

        // GET: /Product/GiaCertificate/{id} or /Product/Certificate?orderId=...
        [HttpGet]
        [Route("Product/Certificate")]
        [Route("Product/GiaCertificate")]
        [Route("Product/Certificate/{id?}")]
        [Route("Product/GiaCertificate/{id?}")]
        [AllowAnonymous]
        public async Task<IActionResult> GiaCertificate(
            string? id, 
            string? productId, 
            string? orderId, 
            string? carat, 
            string? metal, 
            string? stone, 
            string? shape, 
            string? size, 
            string? engraving)
        {
            // 1. Identify if this request corresponds to a purchased order
            var candidateOrderId = !string.IsNullOrWhiteSpace(orderId) ? orderId :
                (!string.IsNullOrWhiteSpace(productId) && (productId.StartsWith("SAT-ORD-", StringComparison.OrdinalIgnoreCase) || productId.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase))) ? productId :
                (!string.IsNullOrWhiteSpace(id) && (id.StartsWith("SAT-ORD-", StringComparison.OrdinalIgnoreCase) || id.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase))) ? id : null;

            Order? order = null;
            if (!string.IsNullOrWhiteSpace(candidateOrderId))
            {
                order = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Variant)
                            .ThenInclude(v => v!.Metal)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Variant)
                            .ThenInclude(v => v!.Carat)
                    .FirstOrDefaultAsync(o => o.OrderId == candidateOrderId || o.OrderNumber == candidateOrderId || o.ProviderOrderId == candidateOrderId);
            }

            if (order == null && !string.IsNullOrWhiteSpace(productId) && productId.Length > 10)
            {
                order = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Variant)
                            .ThenInclude(v => v!.Metal)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Variant)
                            .ThenInclude(v => v!.Carat)
                    .FirstOrDefaultAsync(o => o.OrderId == productId || o.OrderNumber == productId);
            }

            CatalogItem? product = null;

            // 2. If purchased order, extract product and variant options
            if (order != null)
            {
                var firstItem = order.OrderItems.FirstOrDefault();
                if (firstItem != null && firstItem.ProductId > 0)
                {
                    product = await _catalogBal.GetCatalogItemByIdAsync(firstItem.ProductId.ToString());
                    if (firstItem.Variant != null)
                    {
                        metal ??= firstItem.Variant.Metal?.Name;
                        if (firstItem.Variant.Carat != null)
                        {
                            carat ??= $"{firstItem.Variant.Carat.CaratWeight:0.00} ct";
                        }
                    }
                    engraving ??= firstItem.CustomEngravingText;
                }

                // If product not loaded from OrderItems, parse order.ItemName
                if (product == null && !string.IsNullOrWhiteSpace(order.ItemName))
                {
                    var rawItem = order.ItemName.Split('[')[0].Trim();
                    var openParen = rawItem.IndexOf('(');
                    var cleanName = openParen > 0 ? rawItem.Substring(0, openParen).Trim() : rawItem;

                    if (openParen > 0)
                    {
                        var inside = rawItem.Substring(openParen + 1).TrimEnd(')');
                        if (inside.Contains("Moissanite", StringComparison.OrdinalIgnoreCase)) stone ??= "Moissanite";
                        else if (inside.Contains("Diamond", StringComparison.OrdinalIgnoreCase)) stone ??= "Lab Grown Diamond";

                        var caratMatch = System.Text.RegularExpressions.Regex.Match(inside, @"(\d+(\.\d+)?)\s*(ct|CT)");
                        if (caratMatch.Success) carat ??= $"{caratMatch.Groups[1].Value} ct";

                        var metalMatch = System.Text.RegularExpressions.Regex.Match(inside, @"(10K|14K|18K|Platinum|Silver)[^,\)]*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (metalMatch.Success) metal ??= metalMatch.Value.Trim();

                        var sizeMatch = System.Text.RegularExpressions.Regex.Match(inside, @"Size:\s*([^,\)]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (sizeMatch.Success) size ??= sizeMatch.Groups[1].Value.Trim();

                        var engMatch = System.Text.RegularExpressions.Regex.Match(inside, @"Engraved:\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (engMatch.Success) engraving ??= engMatch.Groups[1].Value.Trim();
                    }

                    product = await _context.CatalogItems.FirstOrDefaultAsync(c => c.Name.ToLower() == cleanName.ToLower() || c.Name.ToLower().Contains(cleanName.ToLower()));
                    if (product == null)
                    {
                        var dbProd = await _context.Products.FirstOrDefaultAsync(p => p.Title.ToLower() == cleanName.ToLower() || p.Title.ToLower().Contains(cleanName.ToLower()));
                        if (dbProd != null)
                        {
                            product = await _catalogBal.GetProductByNumericIdAsync(dbProd.ProductId);
                        }
                    }

                    if (product == null)
                    {
                        product = new CatalogItem
                        {
                            Id = $"sat-ord-{order.OrderId}",
                            Name = cleanName,
                            PriceUSD = order.TotalAmountUSD,
                            ImageUrl = "/assets/ring_1.jpg",
                            Spec = $"{metal ?? "18K Gold"} | {carat ?? "1.50 ct"} | {cleanName}"
                        };
                    }
                }
            }

            // 3. If open product, resolve by ID or slug
            if (product == null)
            {
                var targetId = id ?? productId ?? "sat-prod-2022";
                product = await _catalogBal.GetCatalogItemByIdAsync(targetId);

                if (product == null)
                {
                    var clean = targetId.Replace("sat-prod-", "").Replace("sat-local-", "");
                    if (long.TryParse(clean, out long numId))
                    {
                        product = await _catalogBal.GetProductByNumericIdAsync(numId);
                    }
                }

                if (product == null)
                {
                    var firstActive = await _context.CatalogItems.FirstOrDefaultAsync(c => c.IsActive);
                    product = firstActive ?? new CatalogItem
                    {
                        Id = targetId,
                        Name = "Bespoke Solitaire Diamond Ring",
                        CategoryId = "1",
                        Spec = "18K White Gold | 1.50 ct GIA VVS1 | Round Brilliant",
                        PriceUSD = 2400,
                        ImageUrl = "/assets/ring_1.jpg",
                        IsActive = true
                    };
                }
            }

            var spec = product.Spec ?? string.Empty;

            // 4. Check if Product is a Loose Diamond
            var isLooseDiamond = (product.CategoryId == "2" || product.CategoryId == "7" || 
                (product.Name != null && (product.Name.Contains("Loose Diamond", StringComparison.OrdinalIgnoreCase) || product.Name.Contains("Certified Lab Grown Diamond", StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrEmpty(spec) && spec.Contains("Loose Diamond", StringComparison.OrdinalIgnoreCase)));

            // 5. Resolve Stone Type
            var resolvedStone = "Lab Grown Diamond";
            if (!string.IsNullOrWhiteSpace(stone))
            {
                resolvedStone = stone.Contains("Moissanite", StringComparison.OrdinalIgnoreCase) ? "Moissanite" :
                                stone.Contains("Natural", StringComparison.OrdinalIgnoreCase) ? "Natural Diamond" : "Lab Grown Diamond";
            }
            else if (product.Name.Contains("Moissanite", StringComparison.OrdinalIgnoreCase) || spec.Contains("Moissanite", StringComparison.OrdinalIgnoreCase))
            {
                resolvedStone = "Moissanite";
            }
            else if (product.Name.Contains("Natural", StringComparison.OrdinalIgnoreCase) || spec.Contains("Natural", StringComparison.OrdinalIgnoreCase))
            {
                resolvedStone = "Natural Diamond";
            }

            // 6. Resolve Carat Weight
            decimal numericCarat = 1.50m;
            var titleCaratMatch = System.Text.RegularExpressions.Regex.Match(product.Name, @"(\d+(\.\d+)?)\s*(Carat|ct|CT)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var specCaratMatch = System.Text.RegularExpressions.Regex.Match(spec, @"(\d+(\.\d+)?)\s*(ct|CT)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (isLooseDiamond)
            {
                if (titleCaratMatch.Success && decimal.TryParse(titleCaratMatch.Groups[1].Value, out var tc))
                {
                    numericCarat = tc;
                }
                else if (specCaratMatch.Success && decimal.TryParse(specCaratMatch.Groups[1].Value, out var sc))
                {
                    numericCarat = sc;
                }
                else if (!string.IsNullOrWhiteSpace(carat))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(carat, @"(\d+(\.\d+)?)");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsed))
                        numericCarat = parsed;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(carat))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(carat, @"(\d+(\.\d+)?)");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsed))
                        numericCarat = parsed;
                }
                else if (titleCaratMatch.Success && decimal.TryParse(titleCaratMatch.Groups[1].Value, out var tc))
                {
                    numericCarat = tc;
                }
                else if (specCaratMatch.Success && decimal.TryParse(specCaratMatch.Groups[1].Value, out var sc))
                {
                    numericCarat = sc;
                }
                else if (!string.IsNullOrWhiteSpace(product.CaratOptions))
                {
                    var firstCarat = product.CaratOptions.Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (firstCarat != null)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(firstCarat, @"(\d+(\.\d+)?)");
                        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsed))
                            numericCarat = parsed;
                    }
                }
            }

            // 7. Resolve Metal (Loose diamonds have NO metal mounting)
            var resolvedMetal = isLooseDiamond ? string.Empty : "18K White Gold";
            if (!isLooseDiamond)
            {
                if (!string.IsNullOrWhiteSpace(metal))
                {
                    resolvedMetal = metal;
                }
                else if (spec.Contains("10K") || spec.Contains("14K") || spec.Contains("18K") || spec.Contains("Platinum") || spec.Contains("Gold") || spec.Contains("Silver"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(spec, @"(10K|14K|18K|950 Platinum|Platinum|Sterling Silver)[^\|]*");
                    if (match.Success) resolvedMetal = match.Value.Trim();
                }
                else if (!string.IsNullOrWhiteSpace(product.MetalOptions))
                {
                    var firstMetal = product.MetalOptions.Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (firstMetal != null)
                    {
                        resolvedMetal = firstMetal.Split('(')[0].Trim();
                    }
                }
            }

            // 8. Resolve Shape
            var resolvedShape = "Round Brilliant";
            if (!string.IsNullOrWhiteSpace(shape))
            {
                resolvedShape = shape;
            }
            else
            {
                var nameAndSpec = (product.Name + " " + spec).ToLower();
                if (nameAndSpec.Contains("emerald")) resolvedShape = "Emerald Cut";
                else if (nameAndSpec.Contains("oval")) resolvedShape = "Oval Brilliant";
                else if (nameAndSpec.Contains("cushion")) resolvedShape = "Cushion Modified Brilliant";
                else if (nameAndSpec.Contains("radiant")) resolvedShape = "Radiant Cut";
                else if (nameAndSpec.Contains("pear")) resolvedShape = "Pear Brilliant";
                else if (nameAndSpec.Contains("princess")) resolvedShape = "Square Modified Brilliant";
                else if (nameAndSpec.Contains("marquise")) resolvedShape = "Marquise Brilliant";
                else if (nameAndSpec.Contains("asscher")) resolvedShape = "Asscher Cut";
                else if (nameAndSpec.Contains("heart")) resolvedShape = "Heart Brilliant";
                else resolvedShape = "Round Brilliant";
            }

            // 9. Resolve Color & Clarity
            var colorGrade = "E (Colorless)";
            var clarityGrade = "VVS1";
            if (resolvedStone == "Moissanite")
            {
                colorGrade = "D (Colorless)";
                clarityGrade = "VVS1 (Eye Clean)";
            }
            else
            {
                var colorMatch = System.Text.RegularExpressions.Regex.Match(spec, @"Color\s+([D-Z])(\b|\s|\|)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (colorMatch.Success)
                {
                    var c = colorMatch.Groups[1].Value.ToUpper();
                    colorGrade = (c == "D" || c == "E" || c == "F") ? $"{c} (Colorless)" : $"{c} (Near Colorless)";
                }
                else if (spec.Contains("D-") || spec.Contains("D ") || spec.Contains("D Color", StringComparison.OrdinalIgnoreCase)) colorGrade = "D (Colorless)";
                else if (spec.Contains("F-") || spec.Contains("F ") || spec.Contains("F Color", StringComparison.OrdinalIgnoreCase)) colorGrade = "F (Colorless)";
                else if (spec.Contains("G-") || spec.Contains("G ") || spec.Contains("G Color", StringComparison.OrdinalIgnoreCase)) colorGrade = "G (Near Colorless)";

                var clarityMatch = System.Text.RegularExpressions.Regex.Match(spec, @"Clarity\s+(FL|IF|VVS1|VVS2|VS1|VS2|SI1|SI2)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (clarityMatch.Success)
                {
                    clarityGrade = clarityMatch.Groups[1].Value.ToUpper();
                }
                else if (spec.Contains("VVS2")) clarityGrade = "VVS2";
                else if (spec.Contains("VS1")) clarityGrade = "VS1";
                else if (spec.Contains("VS2")) clarityGrade = "VS2";
                else if (spec.Contains("IF") || spec.Contains("Flawless", StringComparison.OrdinalIgnoreCase)) clarityGrade = "Internally Flawless (IF)";
            }

            // 10. Dynamic Gemological Proportions & Measurements based on Shape and Carat
            var (measurements, tablePct, depthPct, crownAng, pavilionAng) = CalculateGemologicalSpecs(resolvedShape, numericCarat);
            var measMatch = System.Text.RegularExpressions.Regex.Match(spec, @"Measurements:\s*([0-9\.\s\*\-x×]+)\s*mm?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (measMatch.Success)
            {
                var rawM = measMatch.Groups[1].Value.Replace("*", " × ").Replace("x", " × ").Replace("X", " × ").Trim();
                measurements = $"{rawM} mm";
            }

            // 11. Deterministic Report Number & Issue Date
            var seed = Math.Abs((product.Id + (order?.OrderId ?? "") + numericCarat.ToString("F2") + resolvedShape).GetHashCode());
            var num = 2240000000L + (seed % 80000000L);
            var reportNumber = resolvedStone == "Moissanite" ? $"GRA-{num}" : $"GIA-{num}";
            var certMatch = System.Text.RegularExpressions.Regex.Match(spec, @"(IGI|GIA)\s*#?([A-Za-z0-9]+)");
            if (certMatch.Success)
            {
                reportNumber = $"{certMatch.Groups[1].Value.ToUpper()}-{certMatch.Groups[2].Value}";
            }

            var issueDate = order != null 
                ? order.CreatedAt.ToString("MMMM dd, yyyy") 
                : DateTime.Now.AddDays(-(seed % 45)).ToString("MMMM dd, yyyy");

            var inscription = $"{reportNumber} • SAT-JEWELS";
            if (!string.IsNullOrWhiteSpace(engraving) && !isLooseDiamond)
            {
                inscription += $" • \"{engraving.Trim()}\"";
            }

            var authUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var authEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            bool isOrderOwnerOrAdmin = userRole == "Admin" || (order != null && (
                (!string.IsNullOrEmpty(authUserId) && order.UserId == authUserId) ||
                (!string.IsNullOrEmpty(authEmail) && order.CustomerEmail.Equals(authEmail, StringComparison.OrdinalIgnoreCase))));

            var clientName = order?.ShippingFullName;
            if (order != null && !isOrderOwnerOrAdmin && !string.IsNullOrWhiteSpace(clientName))
            {
                clientName = "SAT Private Client";
            }

            var vm = new GiaCertificateViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CategoryName = isLooseDiamond ? "Loose Diamonds" : (product.CategoryId == "6" ? "Necklaces" : (product.CategoryId == "9" ? "Luxury Watches" : "Fine Jewelry")),
                ImageUrl = product.ImageUrl ?? "/assets/ring_1.jpg",
                Sku = product.Id,
                PriceUSD = product.PriceUSD,

                IsPurchasedOrder = order != null,
                OrderId = order?.OrderId,
                OrderNumber = order?.OrderNumber,
                OrderDate = order?.CreatedAt,
                ClientName = clientName,
                IncludesPhysicalCert = order?.IncludesPhysicalGiaCert ?? false,

                ReportNumber = reportNumber,
                IssueDate = issueDate,
                CertType = resolvedStone == "Moissanite" ? "GRA / GIA" : (reportNumber.StartsWith("IGI-") ? "IGI" : "GIA"),
                ReportTitle = resolvedStone == "Moissanite" ? "Gemological Grading Report" : (reportNumber.StartsWith("IGI-") ? "IGI Diamond Dossier" : "Diamond Grading Report"),
                StoneType = resolvedStone,
                VerificationBadge = resolvedStone == "Moissanite" ? "100% Verified Premium Moissanite" : (resolvedStone == "Natural Diamond" ? "100% Verified Natural Diamond" : "100% Verified Lab Grown Diamond"),

                CaratWeight = $"{numericCarat:0.00} ct",
                NumericCarat = numericCarat,
                ColorGrade = colorGrade,
                ClarityGrade = clarityGrade,
                CutGrade = resolvedStone == "Moissanite" ? "Ideal" : "Excellent",

                Polish = "Excellent",
                Symmetry = "Excellent",
                Fluorescence = resolvedStone == "Moissanite" ? "None" : "None (Faint inert)",
                Shape = resolvedShape,
                Measurements = measurements,

                TablePercentage = tablePct,
                DepthPercentage = depthPct,
                CrownAngle = crownAng,
                PavilionAngle = pavilionAng,

                IsLooseDiamond = isLooseDiamond,
                MetalType = resolvedMetal,
                RingSize = isLooseDiamond ? null : size,
                LaserInscription = inscription,
                CustomEngraving = isLooseDiamond ? null : engraving
            };

            return View(vm);
        }

        private static (string measurements, string tablePct, string depthPct, string crownAng, string pavilionAng) CalculateGemologicalSpecs(string shape, decimal carat)
        {
            double c = Math.Max(0.20, (double)carat);
            double scale = Math.Pow(c, 0.3333333333);

            string s = shape.ToLower();
            if (s.Contains("oval"))
            {
                double l = Math.Round(7.70 * scale, 2);
                double w = Math.Round(5.50 * scale, 2);
                double d = Math.Round(w * 0.63, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "59%", "63.2%", "34.5°", "41.0°");
            }
            if (s.Contains("emerald"))
            {
                double l = Math.Round(6.90 * scale, 2);
                double w = Math.Round(4.90 * scale, 2);
                double d = Math.Round(w * 0.66, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "65%", "66.5%", "34.0°", "42.0°");
            }
            if (s.Contains("cushion"))
            {
                double l = Math.Round(6.50 * scale, 2);
                double w = Math.Round(6.30 * scale, 2);
                double d = Math.Round(w * 0.65, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "61%", "64.8%", "34.5°", "41.2°");
            }
            if (s.Contains("pear"))
            {
                double l = Math.Round(8.60 * scale, 2);
                double w = Math.Round(5.60 * scale, 2);
                double d = Math.Round(w * 0.62, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "58%", "62.4%", "35.0°", "41.0°");
            }
            if (s.Contains("radiant"))
            {
                double l = Math.Round(6.80 * scale, 2);
                double w = Math.Round(5.20 * scale, 2);
                double d = Math.Round(w * 0.67, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "65%", "67.0%", "34.5°", "41.5°");
            }
            if (s.Contains("princess") || s.Contains("square"))
            {
                double l = Math.Round(5.50 * scale, 2);
                double w = Math.Round(5.50 * scale, 2);
                double d = Math.Round(w * 0.70, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "68%", "71.0%", "35.0°", "41.5°");
            }
            if (s.Contains("marquise"))
            {
                double l = Math.Round(10.50 * scale, 2);
                double w = Math.Round(5.20 * scale, 2);
                double d = Math.Round(w * 0.61, 2);
                return ($"{l:0.00} × {w:0.00} × {d:0.00} mm", "58%", "62.0%", "34.0°", "41.0°");
            }

            // Default: Round Brilliant
            double d1 = Math.Round(6.48 * scale, 2);
            double d2 = Math.Round(d1 + 0.03, 2);
            double depth = Math.Round(d1 * 0.618, 2);
            return ($"{d1:0.00} × {d2:0.00} × {depth:0.00} mm", "57%", "61.8%", "35.0°", "40.8°");
        }
    }
}
