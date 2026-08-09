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
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

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
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

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
            var categories = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

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
            var cat = await _context.Categories.FindAsync(id.ToLower());
            if (cat == null) return false;

            cat.IsActive = active;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCategoryAsync(string id)
        {
            var cat = await _context.Categories.FindAsync(id.ToLower());
            if (cat == null) return false;

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<CatalogItem>> GetAllCatalogItemsAsync()
        {
            return await _context.CatalogItems
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<CatalogItem?> GetCatalogItemByIdAsync(string id)
        {
            return await _context.CatalogItems.FirstOrDefaultAsync(i => i.Id == id);
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
    }
}
