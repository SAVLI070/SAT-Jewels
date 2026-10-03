using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.BAL
{
    public class DashboardStatsDto
    {
        public int TotalCategories { get; set; }
        public int VisibleCategories { get; set; }
        public int TotalProducts { get; set; }
        public int TotalMetals { get; set; }
        public int TotalReviews { get; set; }
        public string Currency { get; set; } = "USD";
        public string DatabaseStatus { get; set; } = "Active";
    }

    public class AdminBal
    {
        private readonly SatJewelDbContext _context;
        private readonly IConfiguration _configuration;

        public AdminBal(SatJewelDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public static string? ExtractCloudinaryPublicId(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.Contains("res.cloudinary.com")) return null;
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var uploadIdx = path.IndexOf("/upload/");
                if (uploadIdx == -1) return null;

                var afterUpload = path.Substring(uploadIdx + "/upload/".Length);
                // Strip transformation parameters and version prefix like /c_scale,w_800/v12345/ or /v12345/
                var match = System.Text.RegularExpressions.Regex.Match(afterUpload, @"(?:.+/)?v\d+/(.+)$");
                if (match.Success)
                {
                    afterUpload = match.Groups[1].Value;
                }
                else
                {
                    afterUpload = System.Text.RegularExpressions.Regex.Replace(afterUpload, @"^(?:[^/]+/)*v\d+/", "");
                }

                var dotIdx = afterUpload.LastIndexOf('.');
                if (dotIdx > 0)
                {
                    afterUpload = afterUpload.Substring(0, dotIdx);
                }
                return Uri.UnescapeDataString(afterUpload);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> DeleteFromCloudinaryAsync(string? imageUrl)
        {
            var publicId = ExtractCloudinaryPublicId(imageUrl);
            if (string.IsNullOrWhiteSpace(publicId)) return false;

            var cloudName = _configuration["Cloudinary:CloudName"] ?? "ihcs8m6o";
            var apiKey = _configuration["Cloudinary:ApiKey"] ?? "826999397858529";
            var apiSecret = _configuration["Cloudinary:ApiSecret"] ?? "S8guO0Os21rzu4vxKAztT39irto";

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
                return false;

            try
            {
                var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
                var cloudinary = new CloudinaryDotNet.Cloudinary(account);
                var deleteParams = new CloudinaryDotNet.Actions.DeletionParams(publicId)
                {
                    Invalidate = true
                };
                var result = await cloudinary.DestroyAsync(deleteParams);
                return result?.Result == "ok";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cloudinary Delete Warning for {publicId}]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteImageAndCleanDatabaseAsync(string? imageUrl, string? productId = null)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return false;

            var cleanUrl = imageUrl.Trim();
            var publicId = ExtractCloudinaryPublicId(cleanUrl);

            // 1. Delete from Cloudinary CDN storage
            bool cloudinaryDeleted = false;
            if (!string.IsNullOrWhiteSpace(publicId))
            {
                cloudinaryDeleted = await DeleteFromCloudinaryAsync(cleanUrl);
            }

            // 2. Remove matching image from product_images table
            try
            {
                var matchingImgs = await _context.ProductImages
                    .Where(i => i.ImagePath == cleanUrl || (!string.IsNullOrEmpty(publicId) && i.ImagePath.Contains(publicId)))
                    .ToListAsync();

                if (matchingImgs.Any())
                {
                    _context.ProductImages.RemoveRange(matchingImgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteImage DB Warning (ProductImages)]: {ex.Message}");
            }

            // 3. Remove and re-link in CatalogItems (ImageUrl & GalleryImages)
            try
            {
                var catalogItems = await _context.CatalogItems
                    .Where(c => c.ImageUrl == cleanUrl || 
                                (c.GalleryImages != null && (c.GalleryImages.Contains(cleanUrl) || (!string.IsNullOrEmpty(publicId) && c.GalleryImages.Contains(publicId)))))
                    .ToListAsync();

                foreach (var item in catalogItems)
                {
                    if (!string.IsNullOrEmpty(item.GalleryImages))
                    {
                        var urls = item.GalleryImages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(u => u.Trim())
                            .Where(u => u != cleanUrl && (string.IsNullOrEmpty(publicId) || !u.Contains(publicId)))
                            .ToList();
                        item.GalleryImages = string.Join(",", urls);

                        if (item.ImageUrl == cleanUrl || (!string.IsNullOrEmpty(publicId) && (item.ImageUrl ?? "").Contains(publicId)))
                        {
                            item.ImageUrl = urls.FirstOrDefault() ?? "/assets/ring_1.jpg";
                        }
                    }
                    else if (item.ImageUrl == cleanUrl)
                    {
                        item.ImageUrl = "/assets/ring_1.jpg";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteImage DB Warning (CatalogItems)]: {ex.Message}");
            }

            // 4. Update Products table if image_path matches
            try
            {
                var productsWithImg = await _context.Products
                    .Where(p => p.ImagePath == cleanUrl || (!string.IsNullOrEmpty(publicId) && p.ImagePath != null && p.ImagePath.Contains(publicId)))
                    .ToListAsync();

                foreach (var p in productsWithImg)
                {
                    var fallbackImg = await _context.ProductImages
                        .Where(i => i.ProductId == p.ProductId && i.ImagePath != cleanUrl && (string.IsNullOrEmpty(publicId) || !i.ImagePath.Contains(publicId)))
                        .Select(i => i.ImagePath)
                        .FirstOrDefaultAsync();
                    p.ImagePath = fallbackImg ?? "/assets/ring_1.jpg";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteImage DB Warning (Products)]: {ex.Message}");
            }

            await _context.SaveChangesAsync();
            return cloudinaryDeleted;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var totalCategories = await _context.Categories.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            if (totalProducts == 0)
            {
                totalProducts = await _context.CatalogItems.CountAsync(i => i.IsActive);
            }
            var totalMetals = await _context.Metals.CountAsync();
            var totalReviews = await _context.ProductReviews.CountAsync();

            return new DashboardStatsDto
            {
                TotalCategories = totalCategories,
                VisibleCategories = totalCategories,
                TotalProducts = totalProducts,
                TotalMetals = totalMetals > 0 ? totalMetals : 10,
                TotalReviews = totalReviews,
                Currency = "USD",
                DatabaseStatus = "AWS RDS Connected"
            };
        }

        public bool CheckAdminAccess(System.Security.Claims.ClaimsPrincipal user)
        {
            if (user == null || user.Identity?.IsAuthenticated != true) return false;
            
            var userRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? user.FindFirst("role")?.Value
                        ?? (user.IsInRole("Admin") || user.IsInRole("admin") ? "Admin" : "");
                        
            var userEmail = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? user.FindFirst("email")?.Value
                         ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                         ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? user.Identity.Name
                         ?? "";
            userEmail = userEmail.Trim().ToLowerInvariant();

            var adminEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "admin@satjewel.com",
                "admin@satjewels.com",
                "satjewels31@gmail.com",
                "farmbridge13@gmail.com",
                "devilsavli332@gmail.com",
                "propzjewels@gmail.com"
            };

            return string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) 
                || user.IsInRole("Admin") 
                || user.IsInRole("admin")
                || adminEmails.Contains(userEmail);
        }

        public async Task EnsureSequencesSyncedAsync()
        {
            try
            {
                var sql = @"
                    DO $$
                    BEGIN
                        BEGIN PERFORM setval(pg_get_serial_sequence('products', 'id'), COALESCE((SELECT MAX(id) FROM products), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('product_variants', 'id'), COALESCE((SELECT MAX(id) FROM product_variants), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('product_images', 'id'), COALESCE((SELECT MAX(id) FROM product_images), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('categories', 'id'), COALESCE((SELECT MAX(id) FROM categories), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('diamond_shapes', 'id'), COALESCE((SELECT MAX(id) FROM diamond_shapes), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('metals', 'id'), COALESCE((SELECT MAX(id) FROM metals), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('carat_options', 'id'), COALESCE((SELECT MAX(id) FROM carat_options), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('dynamic_pricing_rules', 'id'), COALESCE((SELECT MAX(id) FROM dynamic_pricing_rules), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('product_reviews', 'id'), COALESCE((SELECT MAX(id) FROM product_reviews), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                        BEGIN PERFORM setval(pg_get_serial_sequence('order_tracking_history', 'id'), COALESCE((SELECT MAX(id) FROM order_tracking_history), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                    END $$;
                ";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminBal Sequence Sync Note]: {ex.Message}");
            }
        }

        public async Task<Product> CreateProductWithVariantsAsync(CreateProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Product title is required.");

            Product? product = null;
            var newImgUrls = dto.ImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList() ?? new List<string>();

            // Check if editing existing product
            if (!string.IsNullOrWhiteSpace(dto.EditId))
            {
                var cleanId = dto.EditId.Replace("sat-prod-", "").Replace("sat-local-", "").Trim();
                if (long.TryParse(cleanId, out long numericId))
                {
                    product = await _context.Products.FindAsync(numericId);
                }
                if (product == null)
                {
                    product = await _context.Products.FirstOrDefaultAsync(p => p.Title.ToLower() == dto.Title.Trim().ToLower());
                }
            }

            if (product != null)
            {
                // UPDATE existing product
                product.Title = dto.Title.Trim();
                product.Price = dto.PriceUSD;
                product.CategoryId = dto.CategoryId > 0 ? dto.CategoryId : 1;
                product.DiamondShapeId = dto.DiamondShapeId > 0 ? dto.DiamondShapeId : 1;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                // Clean up any replaced old images from Cloudinary storage
                var oldImgs = await _context.ProductImages.Where(i => i.ProductId == product.ProductId).ToListAsync();
                foreach (var oldImg in oldImgs)
                {
                    if (!newImgUrls.Contains(oldImg.ImagePath))
                    {
                        await DeleteFromCloudinaryAsync(oldImg.ImagePath);
                    }
                }

                var existingCatItem = await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == dto.EditId || i.Id == product.ProductId.ToString() || i.Id == $"sat-prod-{product.ProductId}");
                if (existingCatItem != null && !string.IsNullOrEmpty(existingCatItem.GalleryImages))
                {
                    var oldCatImgs = existingCatItem.GalleryImages.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                    foreach (var oldCatImg in oldCatImgs)
                    {
                        if (!newImgUrls.Contains(oldCatImg))
                        {
                            await DeleteFromCloudinaryAsync(oldCatImg);
                        }
                    }
                }

                if (oldImgs.Any()) _context.ProductImages.RemoveRange(oldImgs);

                var oldVars = await _context.ProductVariants.Where(v => v.ProductId == product.ProductId).ToListAsync();
                if (oldVars.Any()) _context.ProductVariants.RemoveRange(oldVars);

                await _context.SaveChangesAsync();
            }
            else
            {
                await EnsureSequencesSyncedAsync();

                // CREATE new product
                var cleanSlug = System.Text.RegularExpressions.Regex.Replace(dto.Title.ToLowerInvariant(), @"[^a-z0-9\-]", "-").Trim('-');
                cleanSlug = System.Text.RegularExpressions.Regex.Replace(cleanSlug, @"\-+", "-");
                if (string.IsNullOrWhiteSpace(cleanSlug)) cleanSlug = "jewelry-item";
                var uniqueSlug = $"{cleanSlug}-{Guid.NewGuid():N}".Substring(0, Math.Min(60, cleanSlug.Length + 10));

                product = new Product
                {
                    Title = dto.Title.Trim(),
                    Slug = uniqueSlug,
                    Price = dto.PriceUSD,
                    CategoryId = dto.CategoryId > 0 ? dto.CategoryId : 1,
                    DiamondShapeId = dto.DiamondShapeId > 0 ? dto.DiamondShapeId : 1,
                    CreatedAt = DateTime.Now
                };

                try
                {
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // If sequence collision occurs, resync sequence and retry
                    await EnsureSequencesSyncedAsync();
                    _context.Entry(product).State = EntityState.Detached;
                    product.ProductId = 0;
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();
                }
            }

            // Save Product Images
            if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
            {
                int order = 1;
                foreach (var imgUrl in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImagePath = imgUrl.Trim(),
                        DisplayOrder = order++
                    });
                }
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    await EnsureSequencesSyncedAsync();
                    await _context.SaveChangesAsync();
                }
            }

            // Save Enabled Variants into product_variants table
            var metalOffsetDict = new Dictionary<long, decimal>();
            var caratOffsetDict = new Dictionary<long, decimal>();

            if (dto.EnabledVariants != null && dto.EnabledVariants.Count > 0)
            {
                var existingMetals = await _context.Metals.ToDictionaryAsync(m => m.Id);
                if (!existingMetals.ContainsKey(11))
                {
                    try
                    {
                        _context.Metals.Add(new Metal
                        {
                            Id = 11,
                            Name = "925 Sterling Silver",
                            Slug = "925-sterling-silver",
                            ColorGroup = "Silver",
                            ColorHex = "#C0C0C0"
                        });
                        await _context.SaveChangesAsync();
                        existingMetals = await _context.Metals.ToDictionaryAsync(m => m.Id);
                    }
                    catch { }
                }

                var existingCarats = await _context.CaratOptions.ToDictionaryAsync(c => c.Id);

                int skuIndex = 100;
                foreach (var varDto in dto.EnabledVariants.Where(v => v.IsEnabled && v.MetalId > 0))
                {
                    if (!existingMetals.ContainsKey(varDto.MetalId)) continue;
                    long? validCaratId = (varDto.CaratId > 0 && existingCarats.ContainsKey(varDto.CaratId)) ? varDto.CaratId : null;

                    decimal varPrice = varDto.PriceOverrideUSD > 0 ? varDto.PriceOverrideUSD : dto.PriceUSD;
                    decimal offset = varPrice - dto.PriceUSD;

                    if (!metalOffsetDict.ContainsKey(varDto.MetalId))
                    {
                        metalOffsetDict[varDto.MetalId] = offset;
                    }

                    if (validCaratId.HasValue && !caratOffsetDict.ContainsKey(validCaratId.Value))
                    {
                        caratOffsetDict[validCaratId.Value] = offset;
                    }

                    var variant = new ProductVariant
                    {
                        ProductId = product.ProductId,
                        MetalId = varDto.MetalId,
                        CaratId = validCaratId,
                        SKU = $"SAT-{product.ProductId}-{varDto.MetalId}-{(validCaratId ?? 0)}-{skuIndex++}",
                        Price = varPrice,
                        StockQuantity = 25,
                        IsAvailable = true
                    };
                    _context.ProductVariants.Add(variant);
                }
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    await EnsureSequencesSyncedAsync();
                    await _context.SaveChangesAsync();
                }
            }

            // Build synchronized MetalOptions & CaratOptions strings & specifications based on category
            string metalOptionsStr = "";
            string caratOptionsStr = "";
            string itemSpec = "";

            if (dto.CategoryId == 9)
            {
                // Category 9: Luxury Watches (Movements: Swiss / Japan, Stones: Moissanite / Lab Grown Diamond)
                var watchVariants = dto.EnabledVariants?.Where(v => v.IsEnabled).ToList() ?? new List<ProductVariantMatrixItemDto>();
                var movList = new List<string>();
                var stoneList = new List<string>();

                // Mov: 102 = Swiss, 101 = Japan
                var swissVar = watchVariants.FirstOrDefault(v => v.MetalId == 102);
                if (swissVar != null)
                {
                    decimal diff = swissVar.PriceOverrideUSD > 0 ? (swissVar.PriceOverrideUSD - dto.PriceUSD) : 0m;
                    movList.Add(diff != 0 ? $"Swiss ({(diff >= 0 ? "+" : "")}{diff:F0} USD)" : "Swiss");
                }
                var japanVar = watchVariants.FirstOrDefault(v => v.MetalId == 101);
                if (japanVar != null)
                {
                    decimal diff = japanVar.PriceOverrideUSD > 0 ? (japanVar.PriceOverrideUSD - dto.PriceUSD) : 0m;
                    movList.Add(diff != 0 ? $"Japan ({(diff >= 0 ? "+" : "")}{diff:F0} USD)" : "Japan");
                }
                if (movList.Count == 0)
                {
                    movList.Add("Swiss");
                    movList.Add("Japan");
                }

                // Stone: 201 = Moissanite, 202 = Lab Grown Diamond
                var moissVar = watchVariants.FirstOrDefault(v => v.CaratId == 201);
                if (moissVar != null)
                {
                    decimal diff = moissVar.PriceOverrideUSD > 0 ? (moissVar.PriceOverrideUSD - dto.PriceUSD) : 0m;
                    stoneList.Add(diff != 0 ? $"Moissanite ({(diff >= 0 ? "+" : "")}{diff:F0} USD)" : "Moissanite");
                }
                var labVar = watchVariants.FirstOrDefault(v => v.CaratId == 202);
                if (labVar != null)
                {
                    decimal diff = labVar.PriceOverrideUSD > 0 ? (labVar.PriceOverrideUSD - dto.PriceUSD) : 0m;
                    stoneList.Add(diff != 0 ? $"Lab Grown Diamond ({(diff >= 0 ? "+" : "")}{diff:F0} USD)" : "Lab Grown Diamond");
                }
                if (stoneList.Count == 0)
                {
                    stoneList.Add("Moissanite");
                    stoneList.Add("Lab Grown Diamond");
                }

                metalOptionsStr = string.Join("|", movList);
                caratOptionsStr = string.Join("|", stoneList);
                itemSpec = $"Luxury Timepiece | 41 mm | Stainless Steel | Sapphire Glass | 50m Water Resistance | {dto.Title.Trim()}";
            }
            else if (dto.CategoryId == 7)
            {
                // Category 7: Certified Loose Diamonds (No metal types or ring carat weights)
                metalOptionsStr = "";
                caratOptionsStr = "";
                itemSpec = $"Certified Loose Diamond | Lab Grown | IGI Certified | {dto.Title.Trim()}";
            }
            else
            {
                // Standard Fine Jewelry Categories 1 - 6, 8, etc.
                var metalIds = dto.EnabledVariants?.Where(v => v.IsEnabled && v.MetalId > 0).Select(v => v.MetalId).Distinct().ToList() ?? new List<long>();
                var caratIds = dto.EnabledVariants?.Where(v => v.IsEnabled && v.CaratId > 0).Select(v => v.CaratId).Distinct().ToList() ?? new List<long>();

                var metalDb = await _context.Metals.Where(m => metalIds.Contains(m.Id)).ToListAsync();
                var caratDb = await _context.CaratOptions.Where(c => caratIds.Contains(c.Id)).ToListAsync();

                var metalStrings = metalDb.Select(m => {
                    var firstVar = dto.EnabledVariants?.FirstOrDefault(v => v.MetalId == m.Id && v.IsEnabled);
                    decimal diff = firstVar != null && firstVar.PriceOverrideUSD > 0 ? (firstVar.PriceOverrideUSD - dto.PriceUSD) : 0m;
                    return diff != 0 ? $"{m.Name} ({(diff >= 0 ? "+" : "")}{diff:F0} USD)" : m.Name;
                }).ToList();

                var caratStrings = caratDb.Select(c => {
                    var firstVar = dto.EnabledVariants?.FirstOrDefault(v => v.CaratId == c.Id && v.IsEnabled);
                    decimal diff = firstVar != null && firstVar.PriceOverrideUSD > 0 ? (firstVar.PriceOverrideUSD - dto.PriceUSD) : 0m;
                    return diff != 0 ? $"{c.Label} ({(diff >= 0 ? "+" : "")}{diff:F0} USD)" : c.Label;
                }).ToList();

                metalOptionsStr = string.Join("|", metalStrings);
                caratOptionsStr = string.Join("|", caratStrings);
                itemSpec = $"Fine Jewelry | {dto.DiamondType} | MoissPrice:{(dto.MoissanitePriceUSD > 0 ? dto.MoissanitePriceUSD : Math.Round(dto.PriceUSD * 0.55m))} | {dto.Title.Trim()}";
            }

            // Also keep CatalogItems table in 100% sync
            var targetCatId = !string.IsNullOrWhiteSpace(dto.EditId) ? dto.EditId : product.ProductId.ToString();
            var catItem = await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == targetCatId || i.Id == product.ProductId.ToString() || i.Id == $"sat-prod-{product.ProductId}");
            
            decimal moissanitePrice = dto.MoissanitePriceUSD > 0 ? dto.MoissanitePriceUSD : Math.Round(dto.PriceUSD * 0.55m);

            if (catItem != null)
            {
                catItem.Name = dto.Title.Trim();
                catItem.PriceUSD = dto.PriceUSD;
                catItem.MoissanitePrice = moissanitePrice;
                catItem.Spec = itemSpec;
                catItem.CategoryId = dto.CategoryId.ToString();
                catItem.MetalOptions = metalOptionsStr;
                catItem.CaratOptions = caratOptionsStr;
                if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
                {
                    catItem.ImageUrl = dto.ImageUrls[0];
                    catItem.GalleryImages = string.Join(",", dto.ImageUrls);
                }
                _context.CatalogItems.Update(catItem);
                await _context.SaveChangesAsync();
            }
            else
            {
                _context.CatalogItems.Add(new CatalogItem
                {
                    Id = $"sat-prod-{product.ProductId}",
                    Name = dto.Title.Trim(),
                    CategoryId = dto.CategoryId.ToString(),
                    PriceUSD = dto.PriceUSD,
                    MoissanitePrice = moissanitePrice,
                    Spec = itemSpec,
                    ImageUrl = dto.ImageUrls != null && dto.ImageUrls.Count > 0 ? dto.ImageUrls[0] : "/assets/ring_1.jpg",
                    GalleryImages = dto.ImageUrls != null ? string.Join(",", dto.ImageUrls) : "",
                    MetalOptions = metalOptionsStr,
                    CaratOptions = caratOptionsStr,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            return product;
        }

        // ==========================================
        // 1. DYNAMIC PRICING ENGINE MANAGEMENT
        // ==========================================
        public async Task<List<DynamicPricingRule>> GetDynamicPricingRulesAsync()
        {
            var rules = await _context.DynamicPricingRules
                .OrderBy(r => r.RuleType)
                .ThenBy(r => r.DisplayOrder)
                .ToListAsync();

            if (rules.Count == 0)
            {
                // Seed standard defaults matching the complete 10 Metals, 9 Carats, and 9 Ring Sizes
                var defaultRules = new List<DynamicPricingRule>
                {
                    // 10 Metals
                    new() { RuleType = "Metal", Code = "10k_yellow_gold", DisplayName = "10K Yellow Gold", PriceOffsetUSD = 0, DisplayOrder = 1, IsActive = true },
                    new() { RuleType = "Metal", Code = "10k_white_gold", DisplayName = "10K White Gold", PriceOffsetUSD = 0, DisplayOrder = 2, IsActive = true },
                    new() { RuleType = "Metal", Code = "10k_rose_gold", DisplayName = "10K Rose Gold", PriceOffsetUSD = 0, DisplayOrder = 3, IsActive = true },
                    new() { RuleType = "Metal", Code = "14k_yellow_gold", DisplayName = "14K Yellow Gold", PriceOffsetUSD = 180, DisplayOrder = 4, IsActive = true },
                    new() { RuleType = "Metal", Code = "14k_white_gold", DisplayName = "14K White Gold", PriceOffsetUSD = 180, DisplayOrder = 5, IsActive = true },
                    new() { RuleType = "Metal", Code = "14k_rose_gold", DisplayName = "14K Rose Gold", PriceOffsetUSD = 180, DisplayOrder = 6, IsActive = true },
                    new() { RuleType = "Metal", Code = "18k_yellow_gold", DisplayName = "18K Yellow Gold", PriceOffsetUSD = 480, DisplayOrder = 7, IsActive = true },
                    new() { RuleType = "Metal", Code = "18k_white_gold", DisplayName = "18K White Gold", PriceOffsetUSD = 480, DisplayOrder = 8, IsActive = true },
                    new() { RuleType = "Metal", Code = "18k_rose_gold", DisplayName = "18K Rose Gold", PriceOffsetUSD = 480, DisplayOrder = 9, IsActive = true },
                    new() { RuleType = "Metal", Code = "950_platinum", DisplayName = "950 Platinum", PriceOffsetUSD = 850, DisplayOrder = 10, IsActive = true },
                    // 9 Carats
                    new() { RuleType = "Carat", Code = "0.50_ct", DisplayName = "0.50 CT", PriceOffsetUSD = 0, DisplayOrder = 1, IsActive = true },
                    new() { RuleType = "Carat", Code = "0.75_ct", DisplayName = "0.75 CT", PriceOffsetUSD = 150, DisplayOrder = 2, IsActive = true },
                    new() { RuleType = "Carat", Code = "1.00_ct", DisplayName = "1.00 CT", PriceOffsetUSD = 350, DisplayOrder = 3, IsActive = true },
                    new() { RuleType = "Carat", Code = "1.25_ct", DisplayName = "1.25 CT", PriceOffsetUSD = 550, DisplayOrder = 4, IsActive = true },
                    new() { RuleType = "Carat", Code = "1.50_ct", DisplayName = "1.50 CT", PriceOffsetUSD = 750, DisplayOrder = 5, IsActive = true },
                    new() { RuleType = "Carat", Code = "2.00_ct", DisplayName = "2.00 CT", PriceOffsetUSD = 1200, DisplayOrder = 6, IsActive = true },
                    new() { RuleType = "Carat", Code = "3.00_ct", DisplayName = "3.00 CT", PriceOffsetUSD = 2400, DisplayOrder = 7, IsActive = true },
                    new() { RuleType = "Carat", Code = "4.00_ct", DisplayName = "4.00 CT", PriceOffsetUSD = 3800, DisplayOrder = 8, IsActive = true },
                    new() { RuleType = "Carat", Code = "5.00_ct", DisplayName = "5.00 CT", PriceOffsetUSD = 5500, DisplayOrder = 9, IsActive = true },
                    // 9 Ring Sizes
                    new() { RuleType = "RingSize", Code = "us_4_0", DisplayName = "US 4.0 (14.9mm)", PriceOffsetUSD = 0, DisplayOrder = 1, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_5_0", DisplayName = "US 5.0 (15.7mm)", PriceOffsetUSD = 0, DisplayOrder = 2, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_6_0", DisplayName = "US 6.0 (16.5mm)", PriceOffsetUSD = 0, DisplayOrder = 3, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_7_0", DisplayName = "US 7.0 (17.3mm)", PriceOffsetUSD = 0, DisplayOrder = 4, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_8_0", DisplayName = "US 8.0 (18.2mm)", PriceOffsetUSD = 0, DisplayOrder = 5, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_9_0", DisplayName = "US 9.0 (19.0mm)", PriceOffsetUSD = 25, DisplayOrder = 6, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_10_0", DisplayName = "US 10.0 (19.8mm)", PriceOffsetUSD = 50, DisplayOrder = 7, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_11_0", DisplayName = "US 11.0 (20.6mm)", PriceOffsetUSD = 75, DisplayOrder = 8, IsActive = true },
                    new() { RuleType = "RingSize", Code = "us_12_0", DisplayName = "US 12.0 (21.4mm)", PriceOffsetUSD = 100, DisplayOrder = 9, IsActive = true },
                };

                _context.DynamicPricingRules.AddRange(defaultRules);
                await _context.SaveChangesAsync();
                return defaultRules;
            }

            // Ensure Silver rule exists in existing databases
            if (!rules.Any(r => r.Code == "silver_925" || r.DisplayName.Contains("Silver", StringComparison.OrdinalIgnoreCase)))
            {
                var silverRule = new DynamicPricingRule
                {
                    RuleType = "Metal",
                    Code = "silver_925",
                    DisplayName = "925 Sterling Silver",
                    PriceOffsetUSD = 0,
                    DisplayOrder = 1,
                    IsActive = true,
                    UpdatedAt = DateTime.Now
                };
                _context.DynamicPricingRules.Add(silverRule);
                await _context.SaveChangesAsync();
                rules.Insert(0, silverRule);
            }

            // Ensure Ring Size rules exist in existing databases
            if (!rules.Any(r => r.RuleType == "RingSize"))
            {
                var defaultRingSizes = new List<DynamicPricingRule>
                {
                    new() { RuleType = "RingSize", Code = "us_4_0", DisplayName = "US 4.0 (14.9mm)", PriceOffsetUSD = 0, DisplayOrder = 1, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_5_0", DisplayName = "US 5.0 (15.7mm)", PriceOffsetUSD = 0, DisplayOrder = 2, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_6_0", DisplayName = "US 6.0 (16.5mm)", PriceOffsetUSD = 0, DisplayOrder = 3, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_7_0", DisplayName = "US 7.0 (17.3mm)", PriceOffsetUSD = 0, DisplayOrder = 4, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_8_0", DisplayName = "US 8.0 (18.2mm)", PriceOffsetUSD = 0, DisplayOrder = 5, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_9_0", DisplayName = "US 9.0 (19.0mm)", PriceOffsetUSD = 25, DisplayOrder = 6, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_10_0", DisplayName = "US 10.0 (19.8mm)", PriceOffsetUSD = 50, DisplayOrder = 7, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_11_0", DisplayName = "US 11.0 (20.6mm)", PriceOffsetUSD = 75, DisplayOrder = 8, IsActive = true, UpdatedAt = DateTime.Now },
                    new() { RuleType = "RingSize", Code = "us_12_0", DisplayName = "US 12.0 (21.4mm)", PriceOffsetUSD = 100, DisplayOrder = 9, IsActive = true, UpdatedAt = DateTime.Now },
                };
                _context.DynamicPricingRules.AddRange(defaultRingSizes);
                await _context.SaveChangesAsync();
                rules.AddRange(defaultRingSizes);
            }

            return rules;
        }

        public async Task<bool> SaveDynamicPricingRulesAsync(List<DynamicPricingRuleDto> rules)
        {
            try
            {
                foreach (var dto in rules)
                {
                    var existing = await _context.DynamicPricingRules.FindAsync(dto.Id);
                    if (existing != null)
                    {
                        existing.PriceOffsetUSD = dto.PriceOffsetUSD;
                        existing.DisplayName = dto.DisplayName;
                        existing.IsActive = dto.IsActive;
                        existing.UpdatedAt = DateTime.Now;
                        _context.DynamicPricingRules.Update(existing);
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.Code))
                    {
                        _context.DynamicPricingRules.Add(new DynamicPricingRule
                        {
                            RuleType = dto.RuleType,
                            Code = dto.Code,
                            DisplayName = dto.DisplayName,
                            PriceOffsetUSD = dto.PriceOffsetUSD,
                            DisplayOrder = dto.DisplayOrder,
                            IsActive = dto.IsActive,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveDynamicPricingRulesAsync Error]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddPricingRuleAsync(DynamicPricingRule rule)
        {
            rule.UpdatedAt = DateTime.Now;
            _context.DynamicPricingRules.Add(rule);
            await _context.SaveChangesAsync();
            return true;
        }

        // ==========================================
        // 2. ORDERS & LIVE TRACKING MANAGEMENT
        // ==========================================
        public class OrderStatusCountsDto
        {
            public int TotalCount { get; set; }
            public int PendingCount { get; set; }
            public int PaidCount { get; set; }
            public int DispatchedCount { get; set; }
            public int InTransitCount { get; set; }
            public int DeliveredCount { get; set; }
        }

        public async Task<OrderStatusCountsDto> GetOrderStatusCountsAsync(string? userId = null, string? userEmail = null)
        {
            // Only genuinely paid/placed orders are displayed at admin side (abandoned/unpaid checkouts are kept in DB for telemetry only)
            var query = _context.Orders.AsNoTracking()
                .Where(o => o.CurrentTrackingStatus != "PaymentPending" && o.OrderStatus != "Pending");

            if (!string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(userEmail))
            {
                var uId = userId?.Trim();
                var email = userEmail?.Trim().ToLower();
                query = query.Where(o => 
                    (!string.IsNullOrEmpty(uId) && o.UserId == uId) || 
                    (!string.IsNullOrEmpty(email) && o.CustomerEmail.ToLower() == email));
            }

            var statusSummaries = await query
                .Select(o => new { o.OrderStatus, o.CurrentTrackingStatus })
                .ToListAsync();

            return new OrderStatusCountsDto
            {
                TotalCount = statusSummaries.Count,
                PendingCount = 0,
                PaidCount = statusSummaries.Count(o => o.OrderStatus.Contains("Paid", StringComparison.OrdinalIgnoreCase) || o.OrderStatus.Contains("Completed", StringComparison.OrdinalIgnoreCase) || o.CurrentTrackingStatus == "OrderPlaced"),
                DispatchedCount = statusSummaries.Count(o => o.OrderStatus.Contains("Dispatched", StringComparison.OrdinalIgnoreCase) || o.OrderStatus.Contains("Booked", StringComparison.OrdinalIgnoreCase) || o.OrderStatus.Contains("Shipped", StringComparison.OrdinalIgnoreCase) || (o.CurrentTrackingStatus != null && (o.CurrentTrackingStatus.Contains("Dispatched", StringComparison.OrdinalIgnoreCase) || o.CurrentTrackingStatus.Contains("Booked", StringComparison.OrdinalIgnoreCase) || o.CurrentTrackingStatus.Contains("Shipped", StringComparison.OrdinalIgnoreCase)))),
                InTransitCount = statusSummaries.Count(o => o.OrderStatus.Contains("Transit", StringComparison.OrdinalIgnoreCase) || (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.Contains("Transit", StringComparison.OrdinalIgnoreCase))),
                DeliveredCount = statusSummaries.Count(o => o.OrderStatus.Contains("Delivered", StringComparison.OrdinalIgnoreCase) || (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.Contains("Delivered", StringComparison.OrdinalIgnoreCase)))
            };
        }

        public async Task<List<Order>> GetAllOrdersWithTrackingAsync(string? statusFilter = null, string? search = null, string? userId = null, string? userEmail = null)
        {
            var query = _context.Orders
                .Include(o => o.TrackingHistory)
                .AsNoTracking()
                .Where(o => o.CurrentTrackingStatus != "PaymentPending" && o.OrderStatus != "Pending");

            if (!string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(userEmail))
            {
                var uId = userId?.Trim();
                var email = userEmail?.Trim().ToLower();
                query = query.Where(o => 
                    (!string.IsNullOrEmpty(uId) && o.UserId == uId) || 
                    (!string.IsNullOrEmpty(email) && o.CustomerEmail.ToLower() == email));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var cleanStatus = statusFilter.Trim().ToLower();
                if (cleanStatus == "paid")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("paid") || 
                        o.OrderStatus.ToLower().Contains("completed") ||
                        o.CurrentTrackingStatus == "OrderPlaced");
                }
                else if (cleanStatus == "pending")
                {
                    query = query.Where(o => o.OrderStatus.ToLower().Contains("pending"));
                }
                else if (cleanStatus == "shipmentbooked" || cleanStatus == "dispatched")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("dispatched") || 
                        o.OrderStatus.ToLower().Contains("booked") ||
                        (o.CurrentTrackingStatus != null && (o.CurrentTrackingStatus.ToLower().Contains("dispatched") || o.CurrentTrackingStatus.ToLower().Contains("booked"))));
                }
                else if (cleanStatus == "intransit" || cleanStatus == "in transit")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("transit") || 
                        (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.ToLower().Contains("transit")));
                }
                else if (cleanStatus == "delivered")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("delivered") || 
                        (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.ToLower().Contains("delivered")));
                }
                else
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower() == cleanStatus || 
                        (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.ToLower() == cleanStatus));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                query = query.Where(o => 
                    o.OrderNumber.ToLower().Contains(q) || 
                    o.CustomerEmail.ToLower().Contains(q) || 
                    o.ShippingFullName.ToLower().Contains(q) || 
                    (o.TrackingNumber != null && o.TrackingNumber.ToLower().Contains(q)) ||
                    o.OrderId.ToLower().Contains(q));
            }

            return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        }

        public async Task<(List<Order> Orders, int TotalCount)> GetOrdersPagedAsync(string? statusFilter = null, string? search = null, string? userId = null, string? userEmail = null, int page = 1, int pageSize = 15)
        {
            var query = _context.Orders
                .Include(o => o.TrackingHistory)
                .AsNoTracking()
                .Where(o => o.CurrentTrackingStatus != "PaymentPending" && o.OrderStatus != "Pending");

            if (!string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(userEmail))
            {
                var uId = userId?.Trim();
                var email = userEmail?.Trim().ToLower();
                query = query.Where(o => 
                    (!string.IsNullOrEmpty(uId) && o.UserId == uId) || 
                    (!string.IsNullOrEmpty(email) && o.CustomerEmail.ToLower() == email));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var cleanStatus = statusFilter.Trim().ToLower();
                if (cleanStatus == "paid")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("paid") || 
                        o.OrderStatus.ToLower().Contains("completed"));
                }
                else if (cleanStatus == "pending")
                {
                    query = query.Where(o => o.OrderStatus.ToLower().Contains("pending"));
                }
                else if (cleanStatus == "shipmentbooked" || cleanStatus == "dispatched" || cleanStatus == "shipped")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("dispatched") || 
                        o.OrderStatus.ToLower().Contains("booked") ||
                        o.OrderStatus.ToLower().Contains("shipped") ||
                        (o.CurrentTrackingStatus != null && (o.CurrentTrackingStatus.ToLower().Contains("dispatched") || o.CurrentTrackingStatus.ToLower().Contains("booked") || o.CurrentTrackingStatus.ToLower().Contains("shipped"))));
                }
                else if (cleanStatus == "intransit" || cleanStatus == "in transit")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("transit") || 
                        (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.ToLower().Contains("transit")));
                }
                else if (cleanStatus == "delivered")
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower().Contains("delivered") || 
                        (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.ToLower().Contains("delivered")));
                }
                else
                {
                    query = query.Where(o => 
                        o.OrderStatus.ToLower() == cleanStatus || 
                        (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.ToLower() == cleanStatus));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                query = query.Where(o => 
                    o.OrderNumber.ToLower().Contains(q) || 
                    o.CustomerEmail.ToLower().Contains(q) || 
                    o.ShippingFullName.ToLower().Contains(q) || 
                    (o.TrackingNumber != null && o.TrackingNumber.ToLower().Contains(q)) ||
                    o.OrderId.ToLower().Contains(q));
            }

            int totalCount = await query.CountAsync();
            var pagedOrders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (pagedOrders, totalCount);
        }

        public async Task<Order?> GetOrderDetailsWithTimelineAsync(string orderId)
        {
            return await _context.Orders
                .Include(o => o.TrackingHistory)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId || o.OrderNumber == orderId);
        }

        // ==========================================
        // 3. USER & CUSTOMER DIRECTORY MANAGEMENT
        // ==========================================
        public class UserWithStatsDto
        {
            public string Id { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Role { get; set; } = "Customer";
            public DateTime CreatedAt { get; set; }
            public int TotalOrders { get; set; }
            public decimal LifetimeSpendUSD { get; set; }
            public string LastOrderNumber { get; set; } = string.Empty;
            public string LastOrderStatus { get; set; } = string.Empty;
        }

        public async Task<List<UserWithStatsDto>> GetAllUsersWithStatsAsync()
        {
            var users = await _context.Users.AsNoTracking().ToListAsync();
            var orders = await _context.Orders.AsNoTracking().ToListAsync();
            var addresses = await _context.UserAddresses.AsNoTracking().ToListAsync();

            var result = new List<UserWithStatsDto>();

            foreach (var u in users)
            {
                var uOrders = orders.Where(o => 
                    (!string.IsNullOrWhiteSpace(o.UserId) && o.UserId == u.Id) || 
                    (!string.IsNullOrWhiteSpace(o.CustomerEmail) && o.CustomerEmail.Equals(u.Email, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                var paidOrders = uOrders.Where(o => 
                    !string.IsNullOrWhiteSpace(o.OrderStatus) && 
                    !o.OrderStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) && 
                    !o.OrderStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                var lastOrder = uOrders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

                var uAddrs = addresses.Where(a => a.UserId == u.Id).ToList();
                var defaultAddr = uAddrs.FirstOrDefault(a => a.IsDefault) ?? uAddrs.FirstOrDefault();

                string resolvedPhone = u.Phone;
                if (string.IsNullOrWhiteSpace(resolvedPhone) && defaultAddr != null && !string.IsNullOrWhiteSpace(defaultAddr.Phone))
                {
                    resolvedPhone = defaultAddr.Phone;
                }
                if (string.IsNullOrWhiteSpace(resolvedPhone) && lastOrder != null && !string.IsNullOrWhiteSpace(lastOrder.ShippingPhone))
                {
                    resolvedPhone = lastOrder.ShippingPhone;
                }

                string resolvedAddress = "";
                if (defaultAddr != null)
                {
                    var parts = new[] { defaultAddr.StreetAddress, defaultAddr.ApartmentSuite, defaultAddr.City, defaultAddr.State, defaultAddr.PostalCode, defaultAddr.Country }
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    resolvedAddress = string.Join(", ", parts);
                }
                else if (lastOrder != null && !string.IsNullOrWhiteSpace(lastOrder.ShippingAddress))
                {
                    resolvedAddress = lastOrder.ShippingAddress;
                }

                result.Add(new UserWithStatsDto
                {
                    Id = u.Id,
                    FullName = string.IsNullOrWhiteSpace(u.FullName) ? "Valued Client" : u.FullName,
                    Email = u.Email,
                    Phone = resolvedPhone,
                    Address = resolvedAddress,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    TotalOrders = uOrders.Count,
                    LifetimeSpendUSD = paidOrders.Sum(o => o.TotalAmountUSD),
                    LastOrderNumber = lastOrder?.OrderNumber ?? "None",
                    LastOrderStatus = lastOrder?.OrderStatus ?? "No Orders"
                });
            }

            // Also include guest buyers who placed orders without formal registration
            var registeredEmails = users.Select(u => u.Email.ToLower()).ToHashSet();
            var guestOrders = orders.Where(o => !string.IsNullOrWhiteSpace(o.CustomerEmail) && !registeredEmails.Contains(o.CustomerEmail.ToLower()))
                                    .GroupBy(o => o.CustomerEmail.ToLower())
                                    .ToList();

            foreach (var g in guestOrders)
            {
                var gOrders = g.ToList();
                var paidOrders = gOrders.Where(o => 
                    !string.IsNullOrWhiteSpace(o.OrderStatus) && 
                    !o.OrderStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) && 
                    !o.OrderStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                var first = gOrders.First();
                var last = gOrders.OrderByDescending(o => o.CreatedAt).First();

                string guestAddr = "";
                if (last != null && !string.IsNullOrWhiteSpace(last.ShippingAddress))
                {
                    guestAddr = last.ShippingAddress;
                }
                else if (first != null && !string.IsNullOrWhiteSpace(first.ShippingAddress))
                {
                    guestAddr = first.ShippingAddress;
                }

                result.Add(new UserWithStatsDto
                {
                    Id = $"guest-{first.OrderId}",
                    FullName = string.IsNullOrWhiteSpace(first.ShippingFullName) ? "Guest Buyer" : first.ShippingFullName,
                    Email = first.CustomerEmail,
                    Phone = first.ShippingPhone,
                    Address = guestAddr,
                    Role = "Guest Customer",
                    CreatedAt = first.CreatedAt,
                    TotalOrders = gOrders.Count,
                    LifetimeSpendUSD = paidOrders.Sum(o => o.TotalAmountUSD),
                    LastOrderNumber = last.OrderNumber,
                    LastOrderStatus = last.OrderStatus
                });
            }

            return result.OrderByDescending(u => u.LifetimeSpendUSD).ThenByDescending(u => u.TotalOrders).ToList();
        }

        public async Task<(List<UserWithStatsDto> Users, int TotalCount)> GetUsersPagedAsync(int page = 1, int pageSize = 15)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;

            int totalCount = await _context.Users.CountAsync();
            var pagedUsers = await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (pagedUsers.Count == 0)
            {
                return (new List<UserWithStatsDto>(), totalCount);
            }

            var userIds = pagedUsers.Select(u => u.Id).ToHashSet();
            var userEmails = pagedUsers.Select(u => u.Email.ToLower()).ToHashSet();

            var relevantOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => (!string.IsNullOrEmpty(o.UserId) && userIds.Contains(o.UserId)) ||
                            (!string.IsNullOrEmpty(o.CustomerEmail) && userEmails.Contains(o.CustomerEmail.ToLower())))
                .ToListAsync();

            var userAddrs = await _context.UserAddresses
                .AsNoTracking()
                .Where(a => userIds.Contains(a.UserId))
                .ToListAsync();

            var result = new List<UserWithStatsDto>();
            foreach (var u in pagedUsers)
            {
                var uOrders = relevantOrders.Where(o =>
                    (!string.IsNullOrWhiteSpace(o.UserId) && o.UserId == u.Id) ||
                    (!string.IsNullOrWhiteSpace(o.CustomerEmail) && o.CustomerEmail.Equals(u.Email, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                var paidOrders = uOrders.Where(o =>
                    !string.IsNullOrWhiteSpace(o.OrderStatus) &&
                    !o.OrderStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) &&
                    !o.OrderStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                var lastOrder = uOrders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

                var uAddrList = userAddrs.Where(a => a.UserId == u.Id).ToList();
                var defaultAddr = uAddrList.FirstOrDefault(a => a.IsDefault) ?? uAddrList.FirstOrDefault();

                string resolvedPhone = u.Phone;
                if (string.IsNullOrWhiteSpace(resolvedPhone) && defaultAddr != null && !string.IsNullOrWhiteSpace(defaultAddr.Phone))
                {
                    resolvedPhone = defaultAddr.Phone;
                }
                if (string.IsNullOrWhiteSpace(resolvedPhone) && lastOrder != null && !string.IsNullOrWhiteSpace(lastOrder.ShippingPhone))
                {
                    resolvedPhone = lastOrder.ShippingPhone;
                }

                string resolvedAddress = "";
                if (defaultAddr != null)
                {
                    var parts = new[] { defaultAddr.StreetAddress, defaultAddr.ApartmentSuite, defaultAddr.City, defaultAddr.State, defaultAddr.PostalCode, defaultAddr.Country }
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    resolvedAddress = string.Join(", ", parts);
                }
                else if (lastOrder != null && !string.IsNullOrWhiteSpace(lastOrder.ShippingAddress))
                {
                    resolvedAddress = lastOrder.ShippingAddress;
                }

                result.Add(new UserWithStatsDto
                {
                    Id = u.Id,
                    FullName = string.IsNullOrWhiteSpace(u.FullName) ? "Valued Client" : u.FullName,
                    Email = u.Email,
                    Phone = resolvedPhone,
                    Address = resolvedAddress,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    TotalOrders = uOrders.Count,
                    LifetimeSpendUSD = paidOrders.Sum(o => o.TotalAmountUSD),
                    LastOrderNumber = lastOrder?.OrderNumber ?? "None",
                    LastOrderStatus = lastOrder?.OrderStatus ?? "No Orders"
                });
            }

            return (result, totalCount);
        }

        public async Task<bool> DeleteProductAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var cleanId = id.Replace("sat-prod-", "").Replace("sat-local-", "").Trim();
            Product? product = null;
            if (long.TryParse(cleanId, out long numericId))
            {
                product = await _context.Products.Include(p => p.Images).Include(p => p.Variants).FirstOrDefaultAsync(p => p.ProductId == numericId);
            }
            if (product == null)
            {
                product = await _context.Products.Include(p => p.Images).Include(p => p.Variants).FirstOrDefaultAsync(p => p.Title.ToLower() == id.Trim().ToLower());
            }

            var catItem = await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == id || i.Id == $"sat-prod-{cleanId}" || i.Id == cleanId);

            // Clean up all product images from Cloudinary storage
            if (product?.Images != null)
            {
                foreach (var img in product.Images)
                {
                    await DeleteFromCloudinaryAsync(img.ImagePath);
                }
            }
            if (catItem != null)
            {
                if (!string.IsNullOrWhiteSpace(catItem.ImageUrl))
                {
                    await DeleteFromCloudinaryAsync(catItem.ImageUrl);
                }
                if (!string.IsNullOrWhiteSpace(catItem.GalleryImages))
                {
                    foreach (var gUrl in catItem.GalleryImages.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        await DeleteFromCloudinaryAsync(gUrl.Trim());
                    }
                }
                _context.CatalogItems.Remove(catItem);
            }

            if (product != null)
            {
                if (product.Images.Any()) _context.ProductImages.RemoveRange(product.Images);
                if (product.Variants.Any()) _context.ProductVariants.RemoveRange(product.Variants);
                _context.Products.Remove(product);
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
