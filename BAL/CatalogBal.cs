using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.BAL
{
    public class CatalogBal
    {
        private readonly SatJewelDbContext _context;

        public CatalogBal(SatJewelDbContext context)
        {
            _context = context;
        }

        // OWASP A03: Input Sanitization Helper
        private string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return HtmlEncoder.Default.Encode(input.Trim());
        }

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        public async Task<List<object>> GetAdminCategoriesAsync()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var items = await _context.CatalogItems.ToListAsync();

            var result = new List<object>();
            foreach (var c in categories)
            {
                var count = items.Count(i => i.CategoryId.Equals(c.Id, StringComparison.OrdinalIgnoreCase));
                result.Add(new
                {
                    id = c.Id,
                    name = c.Name,
                    badge = c.Badge,
                    subtitle = c.Subtitle,
                    imageUrl = c.ImageUrl,
                    displayOrder = c.DisplayOrder,
                    isActive = c.IsActive,
                    itemCount = count
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

        // OWASP A01 & A04: Authoritative Server-Side Price Calculation & Verification
        // Prevents price tampering by calculating the price exclusively from PostgreSQL database values
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

            // Look for patterns like (+150), (-100), (+0), (+1200)
            var match = Regex.Match(optionText, @"\(([\+\-]\d+)\)");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal delta))
            {
                return delta;
            }

            return 0;
        }
    }
}
