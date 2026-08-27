using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.BAL
{
    public class DashboardStatsDto
    {
        public int TotalCategories { get; set; }
        public int VisibleCategories { get; set; }
        public int TotalProducts { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class AdminBal
    {
        private readonly SatJewelDbContext _context;

        public AdminBal(SatJewelDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var categories = await _context.Categories.ToListAsync();
            var items = await _context.CatalogItems.ToListAsync();

            return new DashboardStatsDto
            {
                TotalCategories = categories.Count,
                VisibleCategories = categories.Count(c => c.IsActive),
                TotalProducts = items.Count,
                Currency = "USD"
            };
        }

        public bool CheckAdminAccess(System.Security.Claims.ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true) return false;
            
            var userRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var userEmail = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.ToLower() ?? "";
            
            return userRole == "Admin" || userEmail == "admin" || userEmail == "admin@satjewel.com" || userEmail == "admin@satjewels.com" || user.Identity.Name == "SAT Administrator";
        }

        public async Task<Product> CreateProductWithVariantsAsync(CreateProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Product title is required.");

            Product? product = null;

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

                // Clear old images & variants
                var oldImgs = await _context.ProductImages.Where(i => i.ProductId == product.ProductId).ToListAsync();
                if (oldImgs.Any()) _context.ProductImages.RemoveRange(oldImgs);

                var oldVars = await _context.ProductVariants.Where(v => v.ProductId == product.ProductId).ToListAsync();
                if (oldVars.Any()) _context.ProductVariants.RemoveRange(oldVars);

                await _context.SaveChangesAsync();
            }
            else
            {
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
                    CreatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
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
                await _context.SaveChangesAsync();
            }

            // Save Enabled Variants into product_variants table
            var metalOffsetDict = new Dictionary<long, decimal>();
            var caratOffsetDict = new Dictionary<long, decimal>();

            if (dto.EnabledVariants != null && dto.EnabledVariants.Count > 0)
            {
                int skuIndex = 100;
                foreach (var varDto in dto.EnabledVariants.Where(v => v.IsEnabled && v.MetalId > 0))
                {
                    decimal varPrice = varDto.PriceOverrideUSD > 0 ? varDto.PriceOverrideUSD : dto.PriceUSD;
                    decimal offset = varPrice - dto.PriceUSD;

                    if (!metalOffsetDict.ContainsKey(varDto.MetalId))
                    {
                        metalOffsetDict[varDto.MetalId] = offset;
                    }

                    if (varDto.CaratId > 0 && !caratOffsetDict.ContainsKey(varDto.CaratId))
                    {
                        caratOffsetDict[varDto.CaratId] = offset;
                    }

                    var variant = new ProductVariant
                    {
                        ProductId = product.ProductId,
                        MetalId = varDto.MetalId,
                        CaratId = varDto.CaratId > 0 ? varDto.CaratId : null,
                        SKU = $"SAT-{product.ProductId}-{varDto.MetalId}-{varDto.CaratId}-{skuIndex++}",
                        Price = varPrice,
                        StockQuantity = 25,
                        IsAvailable = true
                    };
                    _context.ProductVariants.Add(variant);
                }
                await _context.SaveChangesAsync();
            }

            // Build synchronized MetalOptions & CaratOptions strings
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

            var metalOptionsStr = string.Join("|", metalStrings);
            var caratOptionsStr = string.Join("|", caratStrings);

            // Also keep CatalogItems table in 100% sync
            var targetCatId = !string.IsNullOrWhiteSpace(dto.EditId) ? dto.EditId : product.ProductId.ToString();
            var catItem = await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == targetCatId || i.Id == product.ProductId.ToString() || i.Id == $"sat-prod-{product.ProductId}");
            
            if (catItem != null)
            {
                catItem.Name = dto.Title.Trim();
                catItem.PriceUSD = dto.PriceUSD;
                catItem.CategoryId = dto.CategoryId.ToString();
                if (!string.IsNullOrWhiteSpace(metalOptionsStr)) catItem.MetalOptions = metalOptionsStr;
                if (!string.IsNullOrWhiteSpace(caratOptionsStr)) catItem.CaratOptions = caratOptionsStr;
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
                    Spec = $"Fine Jewelry | {dto.DiamondType} | {dto.Title.Trim()}",
                    ImageUrl = dto.ImageUrls != null && dto.ImageUrls.Count > 0 ? dto.ImageUrls[0] : "/assets/ring_1.jpg",
                    GalleryImages = dto.ImageUrls != null ? string.Join(",", dto.ImageUrls) : "",
                    MetalOptions = metalOptionsStr,
                    CaratOptions = caratOptionsStr,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
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
                // Seed standard defaults matching Bianca Chiara luxury tiers
                var defaultRules = new List<DynamicPricingRule>
                {
                    // Metals
                    new() { RuleType = "Metal", Code = "10k_gold", DisplayName = "10K Gold", PriceOffsetUSD = 0, DisplayOrder = 1, IsActive = true },
                    new() { RuleType = "Metal", Code = "14k_gold", DisplayName = "14K Gold", PriceOffsetUSD = 180, DisplayOrder = 2, IsActive = true },
                    new() { RuleType = "Metal", Code = "18k_gold", DisplayName = "18K Gold", PriceOffsetUSD = 480, DisplayOrder = 3, IsActive = true },
                    new() { RuleType = "Metal", Code = "platinum_950", DisplayName = "950 Platinum", PriceOffsetUSD = 850, DisplayOrder = 4, IsActive = true },
                    // Carats
                    new() { RuleType = "Carat", Code = "1.00_ct", DisplayName = "1.00 CT", PriceOffsetUSD = 0, DisplayOrder = 1, IsActive = true },
                    new() { RuleType = "Carat", Code = "1.25_ct", DisplayName = "1.25 CT", PriceOffsetUSD = 250, DisplayOrder = 2, IsActive = true },
                    new() { RuleType = "Carat", Code = "1.50_ct", DisplayName = "1.50 CT", PriceOffsetUSD = 450, DisplayOrder = 3, IsActive = true },
                    new() { RuleType = "Carat", Code = "1.75_ct", DisplayName = "1.75 CT", PriceOffsetUSD = 750, DisplayOrder = 4, IsActive = true },
                    new() { RuleType = "Carat", Code = "2.00_ct", DisplayName = "2.00 CT", PriceOffsetUSD = 1100, DisplayOrder = 5, IsActive = true },
                    new() { RuleType = "Carat", Code = "2.50_ct", DisplayName = "2.50 CT", PriceOffsetUSD = 1800, DisplayOrder = 6, IsActive = true },
                    new() { RuleType = "Carat", Code = "3.00_ct", DisplayName = "3.00 CT", PriceOffsetUSD = 2600, DisplayOrder = 7, IsActive = true },
                    new() { RuleType = "Carat", Code = "4.00_ct", DisplayName = "4.00 CT", PriceOffsetUSD = 4200, DisplayOrder = 8, IsActive = true },
                    new() { RuleType = "Carat", Code = "5.00_ct", DisplayName = "5.00 CT", PriceOffsetUSD = 6000, DisplayOrder = 9, IsActive = true },
                };

                _context.DynamicPricingRules.AddRange(defaultRules);
                await _context.SaveChangesAsync();
                return defaultRules;
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
                        existing.UpdatedAt = DateTime.UtcNow;
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
                            UpdatedAt = DateTime.UtcNow
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
            rule.UpdatedAt = DateTime.UtcNow;
            _context.DynamicPricingRules.Add(rule);
            await _context.SaveChangesAsync();
            return true;
        }

        // ==========================================
        // 2. ORDERS & LIVE TRACKING MANAGEMENT
        // ==========================================
        public async Task<List<Order>> GetAllOrdersWithTrackingAsync(string? statusFilter = null, string? search = null)
        {
            var query = _context.Orders
                .Include(o => o.TrackingHistory)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter.ToLower() != "all")
            {
                var cleanStatus = statusFilter.Trim().ToLower();
                query = query.Where(o => o.OrderStatus.ToLower() == cleanStatus || o.CurrentTrackingStatus.ToLower() == cleanStatus);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                query = query.Where(o => 
                    o.OrderNumber.ToLower().Contains(q) || 
                    o.CustomerEmail.ToLower().Contains(q) || 
                    o.ShippingFullName.ToLower().Contains(q) || 
                    o.TrackingNumber.ToLower().Contains(q) ||
                    o.OrderId.ToLower().Contains(q));
            }

            return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
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

            var result = new List<UserWithStatsDto>();

            foreach (var u in users)
            {
                var uOrders = orders.Where(o => 
                    (!string.IsNullOrWhiteSpace(o.UserId) && o.UserId == u.Id) || 
                    (!string.IsNullOrWhiteSpace(o.CustomerEmail) && o.CustomerEmail.Equals(u.Email, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                var paidOrders = uOrders.Where(o => o.OrderStatus == "Paid").ToList();
                var lastOrder = uOrders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

                result.Add(new UserWithStatsDto
                {
                    Id = u.Id,
                    FullName = string.IsNullOrWhiteSpace(u.FullName) ? "Valued Client" : u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
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
                var paidOrders = gOrders.Where(o => o.OrderStatus == "Paid").ToList();
                var first = gOrders.First();
                var last = gOrders.OrderByDescending(o => o.CreatedAt).First();

                result.Add(new UserWithStatsDto
                {
                    Id = $"guest-{first.OrderId}",
                    FullName = string.IsNullOrWhiteSpace(first.ShippingFullName) ? "Guest Buyer" : first.ShippingFullName,
                    Email = first.CustomerEmail,
                    Phone = first.ShippingPhone,
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
    }
}
