using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistApiController : ControllerBase
    {
        private readonly SatJewelDbContext _context;

        public WishlistApiController(SatJewelDbContext context)
        {
            _context = context;
        }

        private string? GetUserId()
        {
            if (User.Identity?.IsAuthenticated != true) return null;
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity.Name;
        }

        // 1. GET SIGNED-IN USER'S WISHLIST ITEMS
        [HttpGet("my-wishlist")]
        public async Task<IActionResult> GetMyWishlist()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Ok(new { success = true, isAuthenticated = false, count = 0, items = new List<WishlistItem>() });
            }

            try
            {
                var items = await _context.WishlistItems
                    .Where(w => w.UserId == userId)
                    .OrderByDescending(w => w.AddedAt)
                    .ToListAsync();

                return Ok(new { success = true, isAuthenticated = true, count = items.Count, items });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // 2. TOGGLE WISHLIST ITEM FOR SIGNED-IN USER
        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleWishlistItem([FromBody] WishlistToggleRequest req)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "Please sign in to save items to your Wishlist." });
            }

            if (req == null || string.IsNullOrWhiteSpace(req.CatalogItemId))
            {
                return BadRequest(new { success = false, message = "Invalid product item." });
            }

            try
            {
                var existing = await _context.WishlistItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.CatalogItemId == req.CatalogItemId);

                if (existing != null)
                {
                    _context.WishlistItems.Remove(existing);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, isSaved = false, message = "Item removed from your Wishlist." });
                }
                else
                {
                    var newItem = new WishlistItem
                    {
                        UserId = userId,
                        CatalogItemId = req.CatalogItemId,
                        ProductName = req.ProductName ?? "Fine Jewelry Product",
                        PriceUSD = req.PriceUSD,
                        ImageUrl = req.ImageUrl ?? "/assets/categories/cat_engagement_rings.png",
                        AddedAt = DateTime.Now
                    };

                    _context.WishlistItems.Add(newItem);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, isSaved = true, message = "Item saved to your Wishlist!" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class WishlistToggleRequest
    {
        public string CatalogItemId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public decimal PriceUSD { get; set; }
        public string? ImageUrl { get; set; }
    }
}
