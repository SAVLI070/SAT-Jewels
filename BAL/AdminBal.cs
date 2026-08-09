using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.BAL
{
    public class AdminBal
    {
        private readonly SatJewelDbContext _context;

        public AdminBal(SatJewelDbContext context)
        {
            _context = context;
        }

        public async Task<object> GetDashboardStatsAsync()
        {
            var categories = await _context.Categories.ToListAsync();
            var items = await _context.CatalogItems.ToListAsync();

            return new
            {
                totalCategories = categories.Count,
                visibleCategories = categories.Count(c => c.IsActive),
                totalProducts = items.Count,
                currency = "USD"
            };
        }

        public bool CheckAdminAccess(System.Security.Claims.ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true) return false;
            
            var userRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var userEmail = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.ToLower() ?? "";
            
            return userRole == "Admin" || userEmail.Contains("admin") || user.Identity.Name == "SAT Administrator";
        }
    }
}
