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
            if (dto.EnabledVariants != null && dto.EnabledVariants.Count > 0)
            {
                int skuIndex = 100;
                foreach (var varDto in dto.EnabledVariants.Where(v => v.IsEnabled && v.MetalId > 0))
                {
                    decimal varPrice = varDto.PriceOverrideUSD > 0 ? varDto.PriceOverrideUSD : dto.PriceUSD;
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

            // Also keep CatalogItems table in 100% sync if present
            if (!string.IsNullOrWhiteSpace(dto.EditId))
            {
                var catItem = await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == dto.EditId || i.Id == product.ProductId.ToString());
                if (catItem != null)
                {
                    catItem.Name = dto.Title.Trim();
                    catItem.PriceUSD = dto.PriceUSD;
                    catItem.CategoryId = dto.CategoryId.ToString();
                    if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
                    {
                        catItem.ImageUrl = dto.ImageUrls[0];
                        catItem.GalleryImages = string.Join(",", dto.ImageUrls);
                    }
                    _context.CatalogItems.Update(catItem);
                    await _context.SaveChangesAsync();
                }
            }

            return product;
        }
    }
}
