using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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

        public CatalogBal(SatJewelDbContext context)
        {
            _context = context;
        }

        private string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return HtmlEncoder.Default.Encode(input.Trim());
        }

        // PUBLIC STOREFRONT CATEGORIES: Returns ONLY categories where IsActive == true
        public async Task<List<CategoryAdminDto>> GetPublicCategoriesAsync()
        {
            var rawCategories = await _context.Categories
                .Where(c => c.IsActive)
                .ToListAsync();
            var categories = rawCategories.OrderBy(c => c.CategoryId).ToList();

            var items = await _context.CatalogItems.Where(i => i.IsActive).ToListAsync();

            var result = new List<CategoryAdminDto>();
            foreach (var c in categories)
            {
                var count = items.Count(i => i.CategoryId.Equals(c.Id, StringComparison.OrdinalIgnoreCase));
                result.Add(new CategoryAdminDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Badge = c.Badge,
                    Subtitle = c.Subtitle,
                    ImageUrl = c.ImageUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = c.IsActive,
                    ItemCount = count
                });
            }

            return result;
        }

        // PUBLIC FULL STORE DATA: Returns ONLY categories where IsActive == true
        public async Task<List<PublicCategoryStoreDto>> GetFullStoreAsync()
        {
            var rawCategories = await _context.Categories
                .Where(c => c.IsActive)
                .ToListAsync();
            var categories = rawCategories.OrderBy(c => c.CategoryId).ToList();

            var items = await _context.CatalogItems.Where(i => i.IsActive).ToListAsync();

            var result = new List<PublicCategoryStoreDto>();
            foreach (var c in categories)
            {
                var catProducts = items.Where(i => i.CategoryId.Equals(c.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                result.Add(new PublicCategoryStoreDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Badge = c.Badge,
                    Subtitle = c.Subtitle,
                    ImageUrl = c.ImageUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = c.IsActive,
                    Products = catProducts
                });
            }

            return result;
        }

        // ADMIN CATEGORIES: Returns ALL categories (Active + Hidden)
        public async Task<List<CategoryAdminDto>> GetAdminCategoriesAsync()
        {
            var rawCategories = await _context.Categories.ToListAsync();
            var categories = rawCategories.OrderBy(c => c.CategoryId).ToList();

            var items = await _context.CatalogItems.ToListAsync();

            var result = new List<CategoryAdminDto>();
            foreach (var c in categories)
            {
                var count = items.Count(i => i.CategoryId.Equals(c.Id, StringComparison.OrdinalIgnoreCase));
                result.Add(new CategoryAdminDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Badge = c.Badge,
                    Subtitle = c.Subtitle,
                    ImageUrl = c.ImageUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = c.IsActive,
                    ItemCount = count
                });
            }

            return result;
        }

        public async Task<bool> AddCategoryAsync(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Id)) return false;

            category.Id = category.Id.Trim().ToLower();
            category.Name = Sanitize(category.Name);
            category.Badge = Sanitize(category.Badge);
            category.Subtitle = Sanitize(category.Subtitle);

            var existing = await _context.Categories.FindAsync(category.Id);
            if (existing != null)
            {
                existing.Name = category.Name;
                existing.Badge = category.Badge;
                existing.Subtitle = category.Subtitle;
                existing.ImageUrl = category.ImageUrl;
                existing.DisplayOrder = category.DisplayOrder;
                existing.IsActive = category.IsActive;
            }
            else
            {
                _context.Categories.Add(category);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleCategoryVisibilityAsync(string id, bool active)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var cleanId = id.Trim().ToLower();
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id.ToLower() == cleanId || c.Name.ToLower() == cleanId);

            if (cat == null && int.TryParse(cleanId, out int numericId))
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == numericId);
            }

            if (cat == null) return false;

            cat.IsActive = active;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCategoryAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var cleanId = id.Trim().ToLower();
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id.ToLower() == cleanId || c.Name.ToLower() == cleanId);

            if (cat == null && int.TryParse(cleanId, out int numericId))
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == numericId);
            }

            if (cat == null) return false;

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();
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

            // Fetch active category IDs
            var activeCatIds = await _context.Categories
                .Where(c => c.IsActive)
                .Select(c => c.Id.ToLower())
                .ToListAsync();

            // 1. If "all", return active products belonging ONLY to active categories on customer storefront
            if (cleanKey == "all")
            {
                try
                {
                    var allDb = await _context.CatalogItems
                        .Where(i => i.IsActive && activeCatIds.Contains(i.CategoryId.ToLower()))
                        .OrderByDescending(i => i.CreatedAt)
                        .ToListAsync();

                    if (allDb.Count > 0) return allDb;
                }
                catch { }

                return LocalStore.GetLocalCategoryProducts("all", webRootPath);
            }

            // 2. Check if specific requested category is active
            var categoryObj = await _context.Categories.FirstOrDefaultAsync(c => c.Id.ToLower() == cleanKey || c.Name.ToLower() == cleanKey.Replace("_", " "));
            if (categoryObj != null && !categoryObj.IsActive)
            {
                // Category is hidden by admin -> return empty list for storefront UI without deleting products
                return new List<CatalogItem>();
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
                    return relationalProducts.Select(p => new CatalogItem
                    {
                        Id = $"sat-prod-{p.ProductId}",
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
                        Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct GIA {p.DiamondClarity} | {p.ProductName}",
                        PriceUSD = p.BasePriceUSD,
                        ImageUrl = p.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImagePath ?? "/assets/ring_1.jpg",
                        GalleryImages = string.Join(",", p.Images.Select(img => img.ImagePath)),
                        IsActive = true,
                        CreatedAt = p.CreatedAt
                    }).ToList();
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
                    return dbItems.Select(p => new CatalogItem
                    {
                        Id = $"sat-prod-{p.ProductId}",
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
                        Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct GIA VVS1",
                        PriceUSD = p.BasePriceUSD,
                        ImageUrl = p.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImagePath ?? "/assets/ring_1.jpg",
                        GalleryImages = string.Join(",", p.Images.Select(i => i.ImagePath)),
                        IsActive = true,
                        CreatedAt = p.CreatedAt
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductsByCategoryAndShapeAsync Error]: {ex.Message}");
            }

            return await GetProductsByNumericIdAsync(categoryId, webRootPath);
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
                    return dbItems.Select(p => new CatalogItem
                    {
                        Id = $"sat-prod-{p.ProductId}",
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
                        Spec = $"{p.DefaultMetalType} | {p.DefaultCaratWeight}ct GIA {p.DiamondClarity}",
                        PriceUSD = p.BasePriceUSD,
                        ImageUrl = p.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImagePath ?? "/assets/ring_1.jpg",
                        GalleryImages = string.Join(",", p.Images.Select(i => i.ImagePath)),
                        IsActive = true,
                        CreatedAt = p.CreatedAt
                    }).ToList();
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
                    var mainImg = p.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.ImagePath ?? "/assets/ring_1.jpg";
                    var allImgs = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImagePath).ToList();

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

                    if (metalVariants.Count == 0)
                    {
                        var defaultMetals = await _context.Metals.OrderBy(m => m.Id).ToListAsync();
                        metalVariants = defaultMetals.Select(m => {
                            decimal offset = m.Name.Contains("14K") ? 180 : m.Name.Contains("18K") ? 480 : m.Name.Contains("Platinum") ? 850 : 0;
                            return offset != 0 ? $"{m.Name} (+{offset:F0} USD)" : m.Name;
                        }).ToList();
                    }

                    if (caratVariants.Count == 0)
                    {
                        var defaultCarats = await _context.CaratOptions.OrderBy(c => c.Id).ToListAsync();
                        caratVariants = defaultCarats.Select(c => {
                            decimal offset = c.Label.Contains("1.5") ? 450 : c.Label.Contains("2.0") ? 1100 : c.Label.Contains("2.5") ? 1800 : c.Label.Contains("3.0") ? 2600 : 0;
                            return offset != 0 ? $"{c.Label} (+{offset:F0} USD)" : c.Label;
                        }).ToList();
                    }

                    return new CatalogItem
                    {
                        Id = $"sat-prod-{p.ProductId}",
                        Name = p.ProductName,
                        CategoryId = p.CategoryId.ToString(),
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
            item.CreatedAt = DateTime.UtcNow;

            _context.CatalogItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<bool> DeleteCatalogItemAsync(string id)
        {
            var item = await _context.CatalogItems.FindAsync(id);
            if (item == null) return false;

            _context.CatalogItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool isValid, decimal serverValidatedPrice, string itemName, string errorMessage)> CalculateServerValidatedPriceAsync(string itemId, string? selectedMetal, string? selectedCarat)
        {
            var item = await GetCatalogItemByIdAsync(itemId);
            if (item == null)
            {
                return (false, 0, "", "Product not found in database.");
            }

            decimal basePrice = item.PriceUSD;
            decimal metalDelta = ParsePriceDelta(selectedMetal);
            decimal caratDelta = ParsePriceDelta(selectedCarat);

            decimal finalAuthoritativePrice = Math.Max(0, basePrice + metalDelta + caratDelta);

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

            return items;
        }
    }
}
