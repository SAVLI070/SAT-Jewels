using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogApiController : ControllerBase
    {
        private readonly SatJewelDbContext _context;

        public CatalogApiController(SatJewelDbContext context)
        {
            _context = context;
        }

        // 1. GET ALL ACTIVE CATEGORIES (Dynamic Main Landing Page Grid)
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                var categoryData = new List<object>();

                foreach (var cat in categories)
                {
                    var itemCount = await _context.CatalogItems.CountAsync(i => i.CategoryId == cat.Id && i.IsActive);
                    categoryData.Add(new
                    {
                        cat.Id,
                        cat.Name,
                        cat.Badge,
                        cat.Subtitle,
                        cat.ImageUrl,
                        cat.DisplayOrder,
                        cat.IsActive,
                        ItemCount = itemCount
                    });
                }

                return Ok(categoryData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace });
            }
        }

        // 1B. GET ALL CATEGORIES FOR ADMIN PANEL (Active + Hidden)
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpGet("admin-categories")]
        public async Task<IActionResult> GetAdminCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                var categoryData = new List<object>();

                foreach (var cat in categories)
                {
                    var itemCount = await _context.CatalogItems.CountAsync(i => i.CategoryId == cat.Id && i.IsActive);
                    categoryData.Add(new
                    {
                        cat.Id,
                        cat.Name,
                        cat.Badge,
                        cat.Subtitle,
                        cat.ImageUrl,
                        cat.DisplayOrder,
                        cat.IsActive,
                        ItemCount = itemCount
                    });
                }

                return Ok(categoryData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 1C. TOGGLE CATEGORY VISIBILITY (Hide / Show on Landing Page without Deleting)
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpPost("categories/{id}/toggle-visibility")]
        public async Task<IActionResult> ToggleCategoryVisibility(string id, [FromQuery] bool active)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { message = "Category not found" });

            category.IsActive = active;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Category '{category.Name}' visibility updated to {(active ? "Visible" : "Hidden")}", isActive = active });
        }

        // 2. ADD NEW CATEGORY FROM ADMIN PANEL
        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] Category category)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(category.Id))
            {
                category.Id = category.Name.ToLower().Replace(" ", "_").Replace("&", "and");
            }
            category.CreatedAt = DateTime.UtcNow;
            category.IsActive = true;

            var existing = await _context.Categories.FindAsync(category.Id);
            if (existing != null)
            {
                existing.Name = category.Name;
                existing.Badge = category.Badge;
                existing.Subtitle = category.Subtitle;
                existing.ImageUrl = category.ImageUrl;
                existing.IsActive = true;
            }
            else
            {
                _context.Categories.Add(category);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"Category '{category.Name}' saved to Neon PostgreSQL DB!", category });
        }

        // 3. DELETE / REMOVE CATEGORY FROM ADMIN PANEL
        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { message = "Category not found" });

            category.IsActive = false; // Soft delete
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Category '{category.Name}' removed from live store!" });
        }

        // 4. GET ALL PRODUCTS
        [HttpGet("items")]
        public async Task<IActionResult> GetCatalogItems(string? categoryId = null)
        {
            var query = _context.CatalogItems.Where(i => i.IsActive);
            if (!string.IsNullOrEmpty(categoryId))
            {
                query = query.Where(i => i.CategoryId == categoryId);
            }

            var items = await query.ToListAsync();
            return Ok(items);
        }

        // 5. ADD NEW PRODUCT ITEM FROM ADMIN PANEL
        [HttpPost("items")]
        public async Task<IActionResult> AddCatalogItem([FromBody] CatalogItem item)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id;
            item.CreatedAt = DateTime.UtcNow;
            item.IsActive = true;

            _context.CatalogItems.Add(item);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Jewelry item saved to Neon PostgreSQL DB!", item });
        }

        // 6. DELETE / REMOVE PRODUCT ITEM
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteCatalogItem(string id)
        {
            var item = await _context.CatalogItems.FindAsync(id);
            if (item == null) return NotFound(new { message = "Item not found" });

            item.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Item removed from database!" });
        }

        // 7. GET FULL STORE DATA (Categories + Items Map)
        [HttpGet("full-store")]
        public async Task<IActionResult> GetFullStore()
        {
            var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();
            var items = await _context.CatalogItems.Where(i => i.IsActive).ToListAsync();

            var result = categories.Select(c => new
            {
                c.Id,
                c.Name,
                c.Badge,
                c.Subtitle,
                c.ImageUrl,
                c.DisplayOrder,
                Products = items.Where(i => i.CategoryId == c.Id).ToList()
            });

            return Ok(result);
        }

        // 8. ADMIN DASHBOARD STATS
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalCategories = await _context.Categories.CountAsync(c => c.IsActive);
            var totalItems = await _context.CatalogItems.CountAsync(i => i.IsActive);
            var totalOrders = await _context.Orders.CountAsync();

            return Ok(new
            {
                totalCategories,
                totalItems,
                totalOrders,
                revenueWeekly = "$35,000",
                showroomsCount = 24,
                portfolioValue = "$60M+"
            });
        }
    }
}
