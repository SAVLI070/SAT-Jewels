using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SAT1.Models;

namespace SAT1.BAL
{
    public class CategoryAdminDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public int ItemCount { get; set; }
    }

    public class PagedCatalogResult
    {
        public List<CatalogItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public class PublicCategoryStoreDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public List<CatalogItem> Products { get; set; } = new List<CatalogItem>();
    }

    public class CatalogBal
    {
        private readonly SatJewelDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly AdminBal _adminBal;
        private readonly IMemoryCache _cache;
        private static bool _categoriesEnsured = false;

        public CatalogBal(SatJewelDbContext context, IConfiguration configuration, AdminBal adminBal, IMemoryCache cache)
        {
            _context = context;
            _configuration = configuration;
            _adminBal = adminBal;
            _cache = cache;
        }

        public static string OptimizeCloudinaryUrl(string? url, int width = 800)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.Contains("res.cloudinary.com") || url.Contains("f_auto"))
                return url ?? string.Empty;
            return url.Replace("/upload/", $"/upload/f_auto/q_auto/w_{width}/");
        }

        public async Task<Dictionary<long, int>> GetShapeCountsByCategoryAsync(long categoryId)
        {
            try
            {
                return await _context.Products
                    .AsNoTracking()
                    .Where(p => p.CategoryId == categoryId && p.DiamondShapeId > 0)
                    .GroupBy(p => p.DiamondShapeId)
                    .Select(g => new { ShapeId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ShapeId, x => x.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetShapeCountsByCategoryAsync Error]: {ex.Message}");
                return new Dictionary<long, int>();
            }
        }

        public void InvalidateCache()
        {
            _cache.Remove("Catalog_PublicCategories");
            _cache.Remove("Catalog_AdminCategories");
            _cache.Remove("Catalog_FullStore");
            _cache.Remove("Global_PricingRules");
            _cache.Remove("Global_Metals");
            _cache.Remove("Global_Carats");
        }

        public async Task<List<DynamicPricingRule>> GetCachedActivePricingRulesAsync()
        {
            const string cacheKey = "Global_PricingRules";
            if (!_cache.TryGetValue(cacheKey, out List<DynamicPricingRule>? rules) || rules == null)
            {
                rules = await _context.DynamicPricingRules.AsNoTracking().Where(r => r.IsActive).ToListAsync();
                _cache.Set(cacheKey, rules, TimeSpan.FromMinutes(15));
            }
            return rules;
        }

        public async Task<List<Metal>> GetCachedMetalsAsync()
        {
            const string cacheKey = "Global_Metals";
            if (!_cache.TryGetValue(cacheKey, out List<Metal>? metals) || metals == null)
            {
                metals = await _context.Metals.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
                _cache.Set(cacheKey, metals, TimeSpan.FromMinutes(30));
            }
            return metals;
        }

        public async Task<List<CaratOption>> GetCachedCaratsAsync()
        {
            const string cacheKey = "Global_Carats";
            if (!_cache.TryGetValue(cacheKey, out List<CaratOption>? carats) || carats == null)
            {
                carats = await _context.CaratOptions.AsNoTracking().OrderBy(c => c.CaratWeight).ToListAsync();
                _cache.Set(cacheKey, carats, TimeSpan.FromMinutes(30));
            }
            return carats;
        }

        private string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return HtmlEncoder.Default.Encode(input.Trim());
        }

        private static readonly Dictionary<string, string> DefaultCloudinaryCategoryImages = new()
        {
            { "1", "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366770/sat_jewels/categories/cat_1_engagement_rings.png" },
            { "2", "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366771/sat_jewels/categories/cat_2_wedding_rings.jpg" },
            { "3", "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366773/sat_jewels/categories/cat_3_bridal_sets.jpg" },
            { "4", "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366775/sat_jewels/categories/cat_4_earrings.jpg" },
            { "5", "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366777/sat_jewels/categories/cat_5_bracelets.jpg" },
            { "6", "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366779/sat_jewels/categories/cat_6_necklaces.jpg" }
        };

        private async Task EnsureDefaultCategoriesAsync()
        {
            if (_categoriesEnsured) return;

            if (!await _context.Categories.AnyAsync())
            {
                var defaultCats = new List<Category>
                {
                    new Category { CategoryId = 1, Name = "ENGAGEMENT RINGS", Slug = "engagement-rings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Engagement Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Bestseller", Subtitle = "Solitaires & Custom Halos", ImageUrl = DefaultCloudinaryCategoryImages["1"], DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.Now },
                    new Category { CategoryId = 2, Name = "WEDDING RINGS", Slug = "wedding-rings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Wedding Ring", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Popular", Subtitle = "Eternity & Wedding Bands", ImageUrl = DefaultCloudinaryCategoryImages["2"], DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.Now },
                    new Category { CategoryId = 3, Name = "BRIDAL SETS", Slug = "bridal-sets", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Bridal Set", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Featured", Subtitle = "Matching Engagement & Band Sets", ImageUrl = DefaultCloudinaryCategoryImages["3"], DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.Now },
                    new Category { CategoryId = 4, Name = "EARRINGS", Slug = "earrings", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Earrings", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Trending", Subtitle = "Diamond Studs & Drop Earrings", ImageUrl = DefaultCloudinaryCategoryImages["4"], DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.Now },
                    new Category { CategoryId = 5, Name = "BRACELETS", Slug = "bracelets", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Bracelets", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "Luxury", Subtitle = "Tennis Bracelets & Bangles", ImageUrl = DefaultCloudinaryCategoryImages["5"], DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.Now },
                    new Category { CategoryId = 6, Name = "NECKLACES", Slug = "necklaces", ParentCategoryId = null, CategoryType = "Main Category", SubCategoryName = "Necklaces", DiamondType = "Lab Grown Diamond", DiamondCutShape = "All Shapes", Badge = "New", Subtitle = "Pendant & Solitaire Necklaces", ImageUrl = DefaultCloudinaryCategoryImages["6"], DisplayOrder = 6, IsActive = true, CreatedAt = DateTime.Now }
                };
                _context.Categories.AddRange(defaultCats);
                await _context.SaveChangesAsync();
            }

            _categoriesEnsured = true;
        }

        // PUBLIC STOREFRONT CATEGORIES: Returns ONLY categories where IsActive == true
        public async Task<List<CategoryAdminDto>> GetPublicCategoriesAsync()
        {
            const string cacheKey = "Catalog_PublicCategories";
            if (_cache.TryGetValue(cacheKey, out List<CategoryAdminDto>? cached) && cached != null)
            {
                return cached;
            }

            await EnsureDefaultCategoriesAsync();
            var allCategories = await _context.Categories.AsNoTracking().ToListAsync();

            var pricingRules = await GetCachedActivePricingRulesAsync();
            var hiddenCodes = pricingRules
                .Where(r => r.RuleType == "CategoryVisibility" && !r.IsActive)
                .Select(r => r.Code.ToLower())
                .ToHashSet();

            var imageRules = pricingRules
                .Where(r => r.RuleType == "CategoryImageUrl")
                .GroupBy(r => r.Code)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);

            var categories = allCategories
                .Where(c => !hiddenCodes.Contains(c.CategoryId.ToString().ToLower()) 
                         && !hiddenCodes.Contains(c.Name.ToLower()) 
                         && !hiddenCodes.Contains(c.Slug.ToLower())
                         && !hiddenCodes.Contains(c.Id.ToLower()))
                .OrderBy(c => c.CategoryId)
                .ToList();

            var productCounts = (await _context.Products.AsNoTracking()
                .GroupBy(p => p.CategoryId)
                .Select(g => new KeyValuePair<long, int>(g.Key, g.Count()))
                .ToListAsync())
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var catalogItemCategoryIds = await _context.CatalogItems.AsNoTracking()
                .Where(i => i.IsActive)
                .Select(i => (i.CategoryId ?? "").ToLower())
                .ToListAsync();

            var catalogItemCounts = catalogItemCategoryIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = new List<CategoryAdminDto>();
            foreach (var c in categories)
            {
                int prodCount = productCounts.GetValueOrDefault(c.CategoryId, 0);
                int catCount = catalogItemCounts.GetValueOrDefault(c.Id.ToLower(), 0) 
                             + catalogItemCounts.GetValueOrDefault(c.Slug.ToLower(), 0)
                             + catalogItemCounts.GetValueOrDefault(c.Name.ToLower(), 0);
                int count = Math.Max(prodCount, catCount);

                var cdnUrl = imageRules.GetValueOrDefault(c.CategoryId.ToString()) ?? imageRules.GetValueOrDefault(c.Id) ?? c.ImageUrl;

                result.Add(new CategoryAdminDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Badge = c.Badge,
                    Subtitle = c.Subtitle,
                    ImageUrl = cdnUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = true,
                    ItemCount = count
                });
            }

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }

        // PUBLIC FULL STORE DATA: Returns ONLY categories where IsActive == true
        public async Task<List<PublicCategoryStoreDto>> GetFullStoreAsync()
        {
            const string cacheKey = "Catalog_FullStore";
            if (_cache.TryGetValue(cacheKey, out List<PublicCategoryStoreDto>? cached) && cached != null)
            {
                return cached;
            }

            await EnsureDefaultCategoriesAsync();
            var allCategories = await _context.Categories.AsNoTracking().ToListAsync();

            var pricingRules = await GetCachedActivePricingRulesAsync();
            var hiddenCodes = pricingRules
                .Where(r => r.RuleType == "CategoryVisibility" && !r.IsActive)
                .Select(r => r.Code.ToLower())
                .ToHashSet();

            var imageRules = pricingRules
                .Where(r => r.RuleType == "CategoryImageUrl")
                .GroupBy(r => r.Code)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);

            var categories = allCategories
                .Where(c => !hiddenCodes.Contains(c.CategoryId.ToString().ToLower()) 
                         && !hiddenCodes.Contains(c.Name.ToLower()) 
                         && !hiddenCodes.Contains(c.Slug.ToLower())
                         && !hiddenCodes.Contains(c.Id.ToLower()))
                .OrderBy(c => c.CategoryId)
                .ToList();

            var items = await _context.CatalogItems.AsNoTracking().Where(i => i.IsActive).ToListAsync();

            var result = new List<PublicCategoryStoreDto>();
            foreach (var c in categories)
            {
                var cdnUrl = imageRules.GetValueOrDefault(c.CategoryId.ToString()) ?? imageRules.GetValueOrDefault(c.Id) ?? c.ImageUrl;
                var catIdLower = c.Id.ToLower();
                var catSlugLower = c.Slug.ToLower();
                var catNameLower = c.Name.ToLower();

                var catProducts = items
                    .Where(i => (i.CategoryId ?? "").ToLower() == catIdLower || (i.CategoryId ?? "").ToLower() == catSlugLower || (i.CategoryId ?? "").ToLower() == catNameLower)
                    .ToList();

                result.Add(new PublicCategoryStoreDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Badge = c.Badge,
                    Subtitle = c.Subtitle,
                    ImageUrl = cdnUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = true,
                    Products = catProducts
                });
            }

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }

        // ADMIN CATEGORIES: Returns ALL categories (Active + Hidden)
        public async Task<List<CategoryAdminDto>> GetAdminCategoriesAsync()
        {
            const string cacheKey = "Catalog_AdminCategories";
            if (_cache.TryGetValue(cacheKey, out List<CategoryAdminDto>? cached) && cached != null)
            {
                return cached;
            }

            await EnsureDefaultCategoriesAsync();
            var rawCategories = await _context.Categories.AsNoTracking().ToListAsync();
            var categories = rawCategories.OrderBy(c => c.CategoryId).ToList();

            var pricingRules = await GetCachedActivePricingRulesAsync();
            var hiddenCodes = pricingRules
                .Where(r => r.RuleType == "CategoryVisibility" && !r.IsActive)
                .Select(r => r.Code.ToLower())
                .ToHashSet();

            var imageRules = pricingRules
                .Where(r => r.RuleType == "CategoryImageUrl")
                .GroupBy(r => r.Code)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);

            var productCounts = (await _context.Products.AsNoTracking()
                .GroupBy(p => p.CategoryId)
                .Select(g => new KeyValuePair<long, int>(g.Key, g.Count()))
                .ToListAsync())
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var catalogItemCategoryIds = await _context.CatalogItems.AsNoTracking()
                .Select(i => (i.CategoryId ?? "").ToLower())
                .ToListAsync();

            var catalogItemCounts = catalogItemCategoryIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = new List<CategoryAdminDto>();
            foreach (var c in categories)
            {
                bool isVisible = !hiddenCodes.Contains(c.CategoryId.ToString().ToLower()) 
                              && !hiddenCodes.Contains(c.Name.ToLower()) 
                              && !hiddenCodes.Contains(c.Slug.ToLower())
                              && !hiddenCodes.Contains(c.Id.ToLower());

                int prodCount = productCounts.GetValueOrDefault(c.CategoryId, 0);
                int catCount = catalogItemCounts.GetValueOrDefault(c.Id.ToLower(), 0) 
                             + catalogItemCounts.GetValueOrDefault(c.Slug.ToLower(), 0)
                             + catalogItemCounts.GetValueOrDefault(c.Name.ToLower(), 0);
                int count = Math.Max(prodCount, catCount);

                var cdnUrl = imageRules.GetValueOrDefault(c.CategoryId.ToString()) ?? imageRules.GetValueOrDefault(c.Id) ?? c.ImageUrl;

                result.Add(new CategoryAdminDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Badge = c.Badge,
                    Subtitle = c.Subtitle,
                    ImageUrl = cdnUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = isVisible,
                    ItemCount = count
                });
            }

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        public async Task<bool> AddCategoryAsync(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name)) return false;

            category.Name = Sanitize(category.Name);
            category.Badge = Sanitize(category.Badge);
            category.Subtitle = Sanitize(category.Subtitle);
            if (string.IsNullOrWhiteSpace(category.Slug))
            {
                category.Slug = category.Name.ToLower().Replace(" ", "-");
            }

            Category? existing = null;
            if (category.CategoryId > 0)
            {
                existing = await _context.Categories.FindAsync(category.CategoryId);
            }
            if (existing == null)
            {
                existing = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == category.Name.ToLower());
            }

            if (existing != null)
            {
                // If previous image was Cloudinary and changed, delete the old image to free up space
                var oldRule = await _context.DynamicPricingRules
                    .FirstOrDefaultAsync(r => r.RuleType == "CategoryImageUrl" && r.Code == existing.CategoryId.ToString());
                var oldImgUrl = oldRule?.DisplayName ?? existing.ImageUrl;

                if (!string.IsNullOrWhiteSpace(oldImgUrl) && oldImgUrl != category.ImageUrl && oldImgUrl.Contains("res.cloudinary.com"))
                {
                    _ = _adminBal.DeleteFromCloudinaryAsync(oldImgUrl);
                }

                existing.Name = category.Name;
                existing.Slug = category.Slug;
                existing.Badge = category.Badge;
                existing.Subtitle = category.Subtitle;
                existing.ImageUrl = category.ImageUrl;
                existing.DisplayOrder = category.DisplayOrder;
                existing.IsActive = category.IsActive;

                // Sync CategoryImageUrl rule in database
                if (oldRule != null)
                {
                    oldRule.DisplayName = category.ImageUrl;
                    _context.DynamicPricingRules.Update(oldRule);
                }
                else
                {
                    _context.DynamicPricingRules.Add(new DynamicPricingRule
                    {
                        RuleType = "CategoryImageUrl",
                        Code = existing.CategoryId.ToString(),
                        DisplayName = category.ImageUrl,
                        PriceOffsetUSD = 0,
                        DisplayOrder = 1,
                        IsActive = true
                    });
                }
            }
            else
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _context.DynamicPricingRules.Add(new DynamicPricingRule
                {
                    RuleType = "CategoryImageUrl",
                    Code = category.CategoryId.ToString(),
                    DisplayName = category.ImageUrl,
                    PriceOffsetUSD = 0,
                    DisplayOrder = 1,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync();
            InvalidateCache();
            return true;
        }

        public async Task<bool> ToggleCategoryVisibilityAsync(string id, bool active)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var cleanId = id.Trim().ToLower();
            Category? cat = null;

            if (long.TryParse(cleanId, out long numericId))
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == numericId);
            }

            if (cat == null)
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == cleanId || c.Slug.ToLower() == cleanId);
            }

            if (cat == null) return false;

            string codeKey = cat.CategoryId.ToString();

            var existingRule = await _context.DynamicPricingRules
                .FirstOrDefaultAsync(r => r.RuleType == "CategoryVisibility" && (r.Code == codeKey || r.Code == cat.Name.ToLower() || r.Code == cat.Slug.ToLower()));

            if (existingRule != null)
            {
                existingRule.IsActive = active;
                existingRule.Code = codeKey;
                existingRule.DisplayName = cat.Name;
                _context.DynamicPricingRules.Update(existingRule);
            }
            else
            {
                _context.DynamicPricingRules.Add(new DynamicPricingRule
                {
                    RuleType = "CategoryVisibility",
                    Code = codeKey,
                    DisplayName = cat.Name,
                    PriceOffsetUSD = 0,
                    DisplayOrder = 1,
                    IsActive = active
                });
            }

            await _context.SaveChangesAsync();
            InvalidateCache();
            return true;
        }

        public async Task<bool> DeleteCategoryAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var cleanId = id.Trim().ToLower();
            Category? cat = null;

            if (long.TryParse(cleanId, out long numericId))
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == numericId);
            }

            if (cat == null)
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == cleanId || c.Slug.ToLower() == cleanId);
            }

            if (cat == null) return false;

            // Delete image from Cloudinary
            var rule = await _context.DynamicPricingRules
                .FirstOrDefaultAsync(r => r.RuleType == "CategoryImageUrl" && r.Code == cat.CategoryId.ToString());
            var imgUrl = rule?.DisplayName ?? cat.ImageUrl;
            if (!string.IsNullOrWhiteSpace(imgUrl) && imgUrl.Contains("res.cloudinary.com"))
            {
                _ = _adminBal.DeleteFromCloudinaryAsync(imgUrl);
            }

            var relatedRules = await _context.DynamicPricingRules
                .Where(r => (r.RuleType == "CategoryImageUrl" || r.RuleType == "CategoryVisibility") && (r.Code == cat.CategoryId.ToString() || r.Code == cat.Name.ToLower() || r.Code == cat.Slug.ToLower()))
                .ToListAsync();
            if (relatedRules.Any())
            {
                _context.DynamicPricingRules.RemoveRange(relatedRules);
            }

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return true;
        }

        // Dynamic Universal Category & Subcategory Filtering (FarmBridge Multi-Way Query Pattern)
        public async Task<List<CatalogItem>> GetProductsByCategoryIdAsync(string? categoryQuery, string webRootPath)
        {
            if (string.IsNullOrWhiteSpace(categoryQuery))
            {
                categoryQuery = "all";
            }

            var cleanKey = categoryQuery.Trim().ToLower().Replace("-", "_").Replace(" ", "_");

            // Fetch hidden category codes from database rules
            var hiddenCodes = await _context.DynamicPricingRules
                .Where(r => r.RuleType == "CategoryVisibility" && !r.IsActive)
                .Select(r => r.Code.ToLower())
                .ToListAsync();

            var allCategories = await _context.Categories.ToListAsync();
            var activeCatIds = allCategories
                .Where(c => !hiddenCodes.Contains(c.CategoryId.ToString().ToLower()) && !hiddenCodes.Contains(c.Name.ToLower()) && !hiddenCodes.Contains(c.Slug.ToLower()) && !hiddenCodes.Contains(c.Id.ToLower()))
                .Select(c => c.Id.ToLower())
                .ToList();

            // 1. If "all", return active products belonging ONLY to active categories on customer storefront
            if (cleanKey == "all")
            {
                try
                {
                    var allDb = await _context.CatalogItems
                        .Where(i => i.IsActive)
                        .OrderByDescending(i => i.CreatedAt)
                        .ToListAsync();

                    var filtered = allDb.Where(i => activeCatIds.Contains(i.CategoryId.ToLower())).ToList();
                    if (filtered.Count > 0) return filtered;
                }
                catch { }

                return LocalStore.GetLocalCategoryProducts("all", webRootPath);
            }

            // 2. Check if specific requested category is active
            var categoryObj = allCategories.FirstOrDefault(c => c.Id.ToLower() == cleanKey || c.Name.ToLower() == cleanKey.Replace("_", " ") || c.Slug.ToLower() == cleanKey);
            if (categoryObj != null)
            {
                bool isCatActive = !hiddenCodes.Contains(categoryObj.CategoryId.ToString().ToLower())
                                && !hiddenCodes.Contains(categoryObj.Name.ToLower())
                                && !hiddenCodes.Contains(categoryObj.Slug.ToLower())
                                && !hiddenCodes.Contains(categoryObj.Id.ToLower());
                if (!isCatActive)
                {
                    // Category is hidden by admin -> return empty list for storefront UI without deleting products
                    return new List<CatalogItem>();
                }
            }

            // 3. Dynamic Query Database via EF Core LINQ parameterized SQL
            try
            {
                var dbProducts = await _context.CatalogItems
                    .Where(i => i.IsActive && (
                        i.CategoryId.ToLower() == cleanKey || 
                        i.CategoryId.ToLower().Replace("-", "_") == cleanKey || 
                        i.CategoryId.ToLower().Replace("_", " ") == cleanKey.Replace("_", " ") ||
                        i.Name.ToLower().Contains(cleanKey.Replace("_", " "))
                    ))
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();

                if (dbProducts.Count > 0)
                {
                    return dbProducts;
                }
            }
            catch { }

            // 4. Dynamic LocalStore provider fallback
            return LocalStore.GetLocalCategoryProducts(categoryQuery, webRootPath);
        }

        private static CatalogItem MapToCatalogItem(Product p)
        {
            var orderedImgs = p.Images != null && p.Images.Count > 0
                ? p.Images.OrderBy(img => img.DisplayOrder)
                          .Select(img => OptimizeCloudinaryUrl(img.ImagePath))
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .Distinct()
                          .ToList()
                : new List<string>();

            if (!string.IsNullOrWhiteSpace(p.ImagePath))
            {
                var optMain = OptimizeCloudinaryUrl(p.ImagePath);
                if (!string.IsNullOrWhiteSpace(optMain) && !orderedImgs.Contains(optMain))
                {
                    orderedImgs.Insert(0, optMain);
                }
            }

            var primaryImg = orderedImgs.FirstOrDefault() 
                             ?? OptimizeCloudinaryUrl(p.ImagePath)
                             ?? OptimizeCloudinaryUrl(p.Images?.FirstOrDefault()?.ImagePath) 
                             ?? "/assets/ring_1.jpg";

            return new CatalogItem
            {
                Id = $"sat-prod-{p.ProductId}",
                Name = p.ProductName,
                CategoryId = p.CategoryId.ToString(),
                DiamondShapeId = p.DiamondShapeId,
                Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct GIA {p.DiamondClarity}",
                PriceUSD = p.BasePriceUSD,
                ImageUrl = primaryImg,
                GalleryImages = string.Join(",", orderedImgs),
                IsActive = true,
                CreatedAt = p.CreatedAt
            };
        }

        // Strongly-Typed Enum Category Filtering (Numeric long ID matching)
        public async Task<List<CatalogItem>> GetProductsByEnumCategoryAsync(RingCategoryEnum categoryEnum, string webRootPath)
        {
            long numericId = (long)categoryEnum;

            try
            {
                var relationalProducts = await _context.Products
                    .Include(p => p.Images)
                    .Where(p => p.CategoryId == numericId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                if (relationalProducts.Count > 0)
                {
                    return relationalProducts.Select(MapToCatalogItem).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductsByEnumCategoryAsync Error]: {ex.Message}");
            }

            return await GetProductsByCategoryIdAsync(categoryEnum.ToString(), webRootPath);
        }

        // Overload accepting numeric long categoryId & shape from UI click
        public async Task<List<CatalogItem>> GetProductsByCategoryAndShapeAsync(long categoryId, string? shape, string webRootPath)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Images)
                    .Where(p => p.CategoryId == categoryId);

                if (!string.IsNullOrWhiteSpace(shape) && shape.ToLower() != "all")
                {
                    var cleanShape = shape.Trim().ToLower();
                    long? targetShapeId = cleanShape switch
                    {
                        "round" or "1" => 1,
                        "oval" or "2" => 2,
                        "emerald" or "3" => 3,
                        "marquise" or "4" => 4,
                        "pear" or "5" => 5,
                        "princess" or "6" => 6,
                        "cushion" or "7" => 7,
                        "radiant" or "8" => 8,
                        "asscher" or "9" => 9,
                        "heart" or "10" => 10,
                        _ => null
                    };

                    if (targetShapeId.HasValue)
                    {
                        query = query.Where(p => p.DiamondShapeId == targetShapeId.Value);
                    }
                    else
                    {
                        query = query.Where(p => p.Title.ToLower().Contains(cleanShape));
                    }
                }

                var dbItems = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

                if (dbItems.Count > 0)
                {
                    return dbItems.Select(MapToCatalogItem).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductsByCategoryAndShapeAsync Error]: {ex.Message}");
            }

            return await GetProductsByNumericIdAsync(categoryId, webRootPath);
        }

        // Fast High-Performance Database-Level Paged Query
        public async Task<PagedCatalogResult> GetCategoryProductsPagedAsync(long categoryId, int page, int pageSize, string? shape, string? sort, string webRootPath, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            try
            {
                var query = _context.Products.AsNoTracking();
                if (categoryId > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var qTerm = search.Trim().ToLower();
                    query = query.Where(p => p.Title.ToLower().Contains(qTerm) || p.Slug.ToLower().Contains(qTerm));
                }

                if (!string.IsNullOrWhiteSpace(shape) && shape.ToLower() != "all")
                {
                    var cleanShape = shape.Trim().ToLower();
                    long? targetShapeId = cleanShape switch
                    {
                        "round" or "1" => 1,
                        "oval" or "2" => 2,
                        "emerald" or "3" => 3,
                        "marquise" or "4" => 4,
                        "pear" or "5" => 5,
                        "princess" or "6" => 6,
                        "cushion" or "7" => 7,
                        "radiant" or "8" => 8,
                        "asscher" or "9" => 9,
                        "heart" or "10" => 10,
                        _ => null
                    };

                    if (targetShapeId.HasValue)
                    {
                        query = query.Where(p => p.DiamondShapeId == targetShapeId.Value);
                    }
                    else
                    {
                        query = query.Where(p => p.Title.ToLower().Contains(cleanShape));
                    }
                }

                // Apply Sorting at the database query level
                query = (sort?.ToLower()) switch
                {
                    "price-asc" or "priceasc" => query.OrderBy(p => p.Price).ThenByDescending(p => p.ProductId),
                    "price-desc" or "pricedesc" => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.ProductId),
                    "alpha-asc" or "alphaasc" => query.OrderBy(p => p.Title).ThenByDescending(p => p.ProductId),
                    "alpha-desc" or "alphadesc" => query.OrderByDescending(p => p.Title).ThenByDescending(p => p.ProductId),
                    "date-asc" or "dateasc" => query.OrderBy(p => p.CreatedAt).ThenBy(p => p.ProductId),
                    "date-desc" or "datedesc" => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId),
                    _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
                };

                int totalCount = await query.CountAsync();

                var dbProducts = await query
                    .Include(p => p.Images)
                    .AsSingleQuery()
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var pagedProducts = dbProducts.Select(MapToCatalogItem).ToList();

                return new PagedCatalogResult
                {
                    Items = pagedProducts,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCategoryProductsPagedAsync Error]: {ex.Message}");
            }

            return new PagedCatalogResult
            {
                Items = new List<CatalogItem>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        // Overload accepting numeric long categoryId from UI click
        public async Task<List<CatalogItem>> GetProductsByNumericIdAsync(long categoryId, string webRootPath)
        {
            if (Enum.IsDefined(typeof(RingCategoryEnum), categoryId))
            {
                return await GetProductsByEnumCategoryAsync((RingCategoryEnum)categoryId, webRootPath);
            }

            try
            {
                var dbItems = await _context.Products
                    .Include(p => p.Images)
                    .Where(p => p.CategoryId == categoryId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                if (dbItems.Count > 0)
                {
                    return dbItems.Select(MapToCatalogItem).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductsByNumericIdAsync Error]: {ex.Message}");
            }

            return await GetProductsByCategoryIdAsync(categoryId.ToString(), webRootPath);
        }

        public async Task<List<CatalogItem>> GetAllCatalogItemsAsync()
        {
            try
            {
                var products = await _context.Products
                    .AsNoTracking()
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new CatalogItem
                    {
                        Id = p.ProductId.ToString(),
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
                        Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct | {p.ProductName}",
                        PriceUSD = p.BasePriceUSD,
                        ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).FirstOrDefault() ?? "/assets/ring_1.jpg",
                        IsActive = true,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();

                if (products.Count > 0)
                {
                    return products;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAllCatalogItemsAsync Error]: {ex.Message}");
            }

            return await _context.CatalogItems
                .AsNoTracking()
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<CatalogItem>> GetCatalogItemsByCategoryAsync(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId) || categoryId.ToLower() == "all")
            {
                return await GetAllCatalogItemsAsync();
            }

            var cleanCat = categoryId.Trim().ToLower();
            long numericCatId = 0;
            long.TryParse(cleanCat, out numericCatId);

            try
            {
                var query = _context.Products.AsNoTracking();
                if (numericCatId > 0)
                {
                    query = query.Where(p => p.CategoryId == numericCatId);
                }
                else
                {
                    query = query.Where(p => p.CategoryId.ToString() == cleanCat);
                }

                var prods = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new CatalogItem
                    {
                        Id = p.ProductId.ToString(),
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
                        Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct | {p.ProductName}",
                        PriceUSD = p.BasePriceUSD,
                        ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).FirstOrDefault() ?? "/assets/ring_1.jpg",
                        IsActive = true,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();

                if (prods.Count > 0) return prods;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCatalogItemsByCategoryAsync Error]: {ex.Message}");
            }

            return await _context.CatalogItems
                .AsNoTracking()
                .Where(i => i.IsActive && i.CategoryId.ToLower() == cleanCat)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<CatalogItem?> GetProductByNumericIdAsync(long productId)
        {
            try
            {
                var p = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Variants!).ThenInclude(v => v.Metal)
                    .Include(p => p.Variants!).ThenInclude(v => v.Carat)
                    .FirstOrDefaultAsync(p => p.ProductId == productId);

                if (p != null)
                {
                    var mainImg = OptimizeCloudinaryUrl(p.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImagePath ?? "/assets/ring_1.jpg");
                    var allImgs = p.Images.OrderBy(i => i.DisplayOrder).Select(i => OptimizeCloudinaryUrl(i.ImagePath)).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

                    var metalVariants = new List<string>();
                    var caratVariants = new List<string>();
                    var variantDtoList = new List<ProductVariantMatrixItemDto>();

                    if (p.Variants != null && p.Variants.Any())
                    {
                        foreach (var v in p.Variants)
                        {
                            if (v.MetalId > 0)
                            {
                                variantDtoList.Add(new ProductVariantMatrixItemDto
                                {
                                    MetalId = v.MetalId,
                                    CaratId = v.CaratId ?? 0,
                                    PriceOverrideUSD = v.Price,
                                    IsEnabled = v.IsAvailable
                                });
                            }
                        }

                        // Build Bianca Chiara format metal differential strings
                        var distinctMetals = p.Variants.Where(v => v.Metal != null).Select(v => v.Metal!).GroupBy(m => m.Id).Select(g => g.First()).ToList();
                        foreach (var m in distinctMetals)
                        {
                            var firstVar = p.Variants.FirstOrDefault(v => v.MetalId == m.Id);
                            decimal offset = firstVar != null && firstVar.Price > 0 ? (firstVar.Price - p.BasePriceUSD) : 0m;
                            metalVariants.Add(offset != 0 ? $"{m.Name} ({(offset >= 0 ? "+" : "")}{offset:F0} USD)" : m.Name);
                        }

                        var distinctCarats = p.Variants.Where(v => v.Carat != null).Select(v => v.Carat!).GroupBy(c => c.Id).Select(g => g.First()).ToList();
                        foreach (var c in distinctCarats)
                        {
                            var firstVar = p.Variants.FirstOrDefault(v => v.CaratId == c.Id);
                            decimal offset = firstVar != null && firstVar.Price > 0 ? (firstVar.Price - p.BasePriceUSD) : 0m;
                            caratVariants.Add(offset != 0 ? $"{c.Label} ({(offset >= 0 ? "+" : "")}{offset:F0} USD)" : c.Label);
                        }
                    }

                    var pricingRules = await GetCachedActivePricingRulesAsync();
                    var defaultMetals = await GetCachedMetalsAsync();
                    metalVariants = defaultMetals.Select(m => {
                        var rule = pricingRules.FirstOrDefault(r => r.RuleType == "Metal" && (
                            (m.Name.Contains("10K") && r.Code.Contains("10k")) ||
                            (m.Name.Contains("14K") && r.Code.Contains("14k")) ||
                            (m.Name.Contains("18K") && r.Code.Contains("18k")) ||
                            (m.Name.Contains("Platinum") && r.Code.Contains("platinum")) ||
                            (m.Name.Contains("Silver") && r.Code.Contains("silver"))
                        ));
                        decimal offset = rule?.PriceOffsetUSD ?? (m.Name.Contains("14K") ? 180 : m.Name.Contains("18K") ? 480 : m.Name.Contains("Platinum") ? 850 : 0);
                        return $"{m.Name} (+{offset:F0} USD)";
                    }).ToList();

                    var defaultCarats = await GetCachedCaratsAsync();
                    caratVariants = defaultCarats.Select(c => {
                        var rule = pricingRules.FirstOrDefault(r => r.RuleType == "Carat" && (
                            r.DisplayName.Contains(c.CaratWeight.ToString("0.00")) || 
                            r.Code.Contains(c.CaratWeight.ToString("0.00").Replace(".", "_")) ||
                            c.Label.Contains(r.DisplayName.Replace(" CT", "").Trim())
                        ));
                        decimal offset = rule?.PriceOffsetUSD ?? (c.CaratWeight >= 3.0m ? 2600 : c.CaratWeight >= 2.0m ? 1100 : c.CaratWeight >= 1.5m ? 450 : 0);
                        return $"{c.Label} (+{offset:F0} USD)";
                    }).ToList();

                    return new CatalogItem
                    {
                        Id = $"sat-prod-{p.ProductId}",
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
                        DiamondShapeId = p.DiamondShapeId,
                        Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct GIA {p.DiamondClarity} | {p.ProductName}",
                        PriceUSD = p.BasePriceUSD,
                        ImageUrl = mainImg,
                        GalleryImages = string.Join(",", allImgs),
                        MetalOptions = string.Join("|", metalVariants),
                        CaratOptions = string.Join("|", caratVariants),
                        IsActive = true,
                        CreatedAt = p.CreatedAt,
                        Variants = variantDtoList
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductByNumericIdAsync Error]: {ex.Message}");
            }

            return null;
        }

        public async Task<CatalogItem?> GetCatalogItemByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            // 1. Try numeric ProductId extraction (e.g., sat-prod-101 or 101)
            var cleanId = id.Replace("sat-prod-", "").Replace("sat-local-", "");
            if (long.TryParse(cleanId, out long numericProductId))
            {
                var dbProd = await GetProductByNumericIdAsync(numericProductId);
                if (dbProd != null) return dbProd;
            }

            // 2. Query CatalogItems table by primary key ID
            var catalogItem = await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == id);
            if (catalogItem == null)
            {
                // 3. Query CatalogItems by Product Name or URL Slug from Neon PostgreSQL
                var slugClean = id.Replace("-", " ").Replace("_", " ").Trim().ToLower();
                catalogItem = await _context.CatalogItems.FirstOrDefaultAsync(i => 
                    i.IsActive && (
                        i.Name.ToLower() == id.ToLower() ||
                        i.Name.ToLower() == slugClean ||
                        i.Name.ToLower().Replace(" ", "-") == id.ToLower() ||
                        i.Name.ToLower().Contains(slugClean)
                    ));
            }

            if (catalogItem != null)
            {
                // Query dedicated MetalOptions and CaratOptions database tables
                var dbMetals = (await _context.MetalOptions
                    .Where(m => m.CatalogItemId == catalogItem.Id)
                    .ToListAsync())
                    .OrderBy(m => m.DisplayOrder)
                    .ToList();

                var dbCarats = (await _context.CaratOptions
                    .Where(c => c.CatalogItemId == catalogItem.Id)
                    .ToListAsync())
                    .OrderBy(c => c.DisplayOrder)
                    .ToList();

                if (dbMetals.Any())
                {
                    catalogItem.MetalOptions = string.Join("|", dbMetals.Select(m => $"{m.MetalName} ({(m.PriceOffsetUSD >= 0 ? "+" : "")}{m.PriceOffsetUSD:F0})"));
                }
                if (dbCarats.Any())
                {
                    catalogItem.CaratOptions = string.Join("|", dbCarats.Select(c => $"{c.CaratLabel} ({(c.PriceOffsetUSD >= 0 ? "+" : "")}{c.PriceOffsetUSD:F0})"));
                }

                return catalogItem;
            }

            return null;
        }

        public async Task<CatalogItem> AddCatalogItemAsync(CatalogItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = Guid.NewGuid().ToString();
            }

            item.Name = Sanitize(item.Name);
            item.Spec = Sanitize(item.Spec);
            item.CreatedAt = DateTime.Now;

            _context.CatalogItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<bool> DeleteCatalogItemAsync(string id)
        {
            return await _adminBal.DeleteProductAsync(id);
        }

        public async Task<(bool isValid, decimal serverValidatedPrice, string itemName, string errorMessage)> CalculateServerValidatedPriceAsync(string itemId, string? selectedMetal, string? selectedCarat, string? selectedRingSize = null, string? selectedStone = null)
        {
            var item = await GetCatalogItemByIdAsync(itemId);
            if (item == null)
            {
                return (false, 0, "", "Product not found in database.");
            }

            decimal basePrice = (selectedStone != null && selectedStone.Contains("Moissanite", StringComparison.OrdinalIgnoreCase)) 
                ? item.MoissanitePriceUSD 
                : item.PriceUSD;

            decimal metalDelta = ParsePriceDelta(selectedMetal);
            decimal caratDelta = ParsePriceDelta(selectedCarat);
            decimal ringSizeDelta = ParsePriceDelta(selectedRingSize);

            decimal finalAuthoritativePrice = Math.Max(0, basePrice + metalDelta + caratDelta + ringSizeDelta);

            return (true, finalAuthoritativePrice, item.Name, "");
        }

        private decimal ParsePriceDelta(string? optionText)
        {
            if (string.IsNullOrWhiteSpace(optionText)) return 0;

            var match = Regex.Match(optionText, @"\(([\+\-]\d+)\)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal delta))
            {
                return delta;
            }

            return 0;
        }

        public async Task<List<CatalogItem>> SearchProductsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return await GetAllCatalogItemsAsync();
            }

            var q = query.Trim().ToLower();
            var items = await _context.CatalogItems
                .Where(i => i.IsActive && (
                    i.Name.ToLower().Contains(q) ||
                    (i.Spec != null && i.Spec.ToLower().Contains(q)) ||
                    (i.CategoryId != null && i.CategoryId.ToLower().Contains(q)) ||
                    (i.MetalOptions != null && i.MetalOptions.ToLower().Contains(q))
                ))
                .OrderByDescending(i => i.CreatedAt)
                .Take(20)
                .ToListAsync();

            if (items.Count == 0)
            {
                try
                {
                    var fallbackProducts = await _context.Products
                        .AsNoTracking()
                        .Include(p => p.Images)
                        .Where(p => p.Title.ToLower().Contains(q) || p.Slug.ToLower().Contains(q))
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(20)
                        .Select(p => new CatalogItem
                        {
                            Id = p.ProductId.ToString(),
                            Name = p.ProductName,
                            CategoryId = p.CategoryId.ToString(),
                            Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct | {p.ProductName}",
                            PriceUSD = p.BasePriceUSD,
                            ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).FirstOrDefault() ?? p.ImagePath ?? "/assets/ring_1.jpg",
                            IsActive = true,
                            CreatedAt = p.CreatedAt
                        })
                        .ToListAsync();

                    if (fallbackProducts.Count > 0)
                    {
                        return fallbackProducts;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SearchProductsAsync Fallback Error]: {ex.Message}");
                }
            }

            return items;
        }
    }
}
