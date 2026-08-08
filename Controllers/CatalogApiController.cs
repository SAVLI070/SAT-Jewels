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

        [HttpGet("items")]
        public async Task<IActionResult> GetCatalogItems()
        {
            var items = await _context.CatalogItems.Where(i => i.IsActive).ToListAsync();
            return Ok(items);
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddCatalogItem([FromBody] CatalogItem item)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            item.Id = Guid.NewGuid().ToString();
            item.CreatedAt = DateTime.UtcNow;
            _context.CatalogItems.Add(item);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Item saved to database!", item });
        }

        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteCatalogItem(string id)
        {
            var item = await _context.CatalogItems.FindAsync(id);
            if (item == null) return NotFound(new { message = "Item not found" });

            item.IsActive = false; // Soft delete
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Item removed from database!" });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalItems = await _context.CatalogItems.CountAsync(i => i.IsActive);
            var totalOrders = await _context.Orders.CountAsync();

            return Ok(new
            {
                totalItems,
                totalOrders,
                revenueWeekly = "₹28,50,000",
                showroomsCount = 24,
                portfolioValue = "₹500 Cr+"
            });
        }
    }
}
