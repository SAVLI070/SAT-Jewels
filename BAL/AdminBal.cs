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
    }
}
