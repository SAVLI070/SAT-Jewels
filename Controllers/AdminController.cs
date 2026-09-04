using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;
using SAT1.Models;

namespace SAT1.Controllers
{
    [Route("admin")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AdminController : Controller
    {
        private readonly AdminBal _adminBal;

        public AdminController(AdminBal adminBal)
        {
            _adminBal = adminBal;
        }

        private bool CheckAccess()
        {
            return _adminBal.CheckAdminAccess(User);
        }

        private IActionResult HandleUnauthorized()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.Message = $"You are currently signed in as customer '{User.Identity.Name}'. The Admin Portal requires Administrator privileges. Please sign out and log in with your Admin credentials.";
                return View("~/Views/Shared/RestrictedAccess.cshtml");
            }

            return Redirect("/Account/SignIn?returnUrl=" + System.Net.WebUtility.UrlEncode(Request.Path));
        }

        [HttpGet("")]
        [HttpGet("index")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Index()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            var stats = await _adminBal.GetDashboardStatsAsync();
            ViewBag.Title = "Dashboard Overview";
            return View("Index", stats);
        }

        [HttpGet("categories")]
        public IActionResult Categories()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Jewelry Category Management";
            return View();
        }

        [HttpGet("catalog")]
        public IActionResult Catalog()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Live Jewelry Catalog Table";
            return View();
        }

        [HttpGet("addproduct")]
        public IActionResult AddProduct()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Publish New Collection Item";
            return View();
        }

        [HttpGet("orders")]
        public async Task<IActionResult> Orders(string? status, string? q)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Customer Orders & Live Tracking";
            var allOrders = await _adminBal.GetAllOrdersWithTrackingAsync("All", null);
            var filteredOrders = await _adminBal.GetAllOrdersWithTrackingAsync(status, q);
            
            ViewBag.TotalCount = allOrders.Count;
            ViewBag.PaidCount = allOrders.Count(o => o.OrderStatus.Contains("Paid", StringComparison.OrdinalIgnoreCase) || o.OrderStatus.Contains("Completed", StringComparison.OrdinalIgnoreCase));
            ViewBag.DispatchedCount = allOrders.Count(o => o.OrderStatus.Contains("Dispatched", StringComparison.OrdinalIgnoreCase) || o.OrderStatus.Contains("Booked", StringComparison.OrdinalIgnoreCase) || (o.CurrentTrackingStatus != null && (o.CurrentTrackingStatus.Contains("Dispatched", StringComparison.OrdinalIgnoreCase) || o.CurrentTrackingStatus.Contains("Booked", StringComparison.OrdinalIgnoreCase))));
            ViewBag.InTransitCount = allOrders.Count(o => o.OrderStatus.Contains("Transit", StringComparison.OrdinalIgnoreCase) || (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.Contains("Transit", StringComparison.OrdinalIgnoreCase)));
            ViewBag.DeliveredCount = allOrders.Count(o => o.OrderStatus.Contains("Delivered", StringComparison.OrdinalIgnoreCase) || (o.CurrentTrackingStatus != null && o.CurrentTrackingStatus.Contains("Delivered", StringComparison.OrdinalIgnoreCase)));

            ViewBag.StatusFilter = status ?? "All";
            ViewBag.SearchQuery = q ?? "";
            return View(filteredOrders);
        }

        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Customer Accounts & Directory";
            var users = await _adminBal.GetAllUsersWithStatsAsync();
            return View(users);
        }

        [HttpGet("pricing")]
        public async Task<IActionResult> Pricing()
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Metal & Carat Dynamic Pricing Rules";
            var rules = await _adminBal.GetDynamicPricingRulesAsync();
            return View(rules);
        }

        [HttpGet("reviews")]
        public async Task<IActionResult> Reviews([FromServices] ReviewBal reviewBal, string? status, int page = 1, int pageSize = 12)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Product Customer Reviews Moderation";
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            var allReviews = await reviewBal.GetAllReviewsAsync(status);
            int totalCount = allReviews.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            var pagedReviews = allReviews.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.StatusFilter = status ?? "All";
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalPages;
            ViewBag.AllReviewsCount = (await reviewBal.GetAllReviewsAsync("All")).Count;

            return View(pagedReviews);
        }

        [HttpGet("shippingexceptions")]
        public async Task<IActionResult> ShippingExceptions([FromServices] SAT1.DAL.OrderTrackingRepository trackingRepo)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Shipping Alerts & Carrier Exceptions";
            var exceptions = await trackingRepo.GetShippingExceptionsAsync();
            return View(exceptions);
        }

        // ==========================================
        // ADMIN AJAX POST ENDPOINTS
        // ==========================================
        [HttpPost("api/pricing/save")]
        public async Task<IActionResult> SavePricing([FromBody] List<DynamicPricingRuleDto> rules)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            var success = await _adminBal.SaveDynamicPricingRulesAsync(rules);
            return Json(new { success, message = success ? "Dynamic pricing rules updated successfully across store!" : "Failed to save pricing." });
        }

        [HttpPost("api/pricing/add")]
        public async Task<IActionResult> AddPricingRule([FromBody] DynamicPricingRule rule)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            var success = await _adminBal.AddPricingRuleAsync(rule);
            return Json(new { success, message = success ? "New pricing rule added successfully!" : "Failed to add rule." });
        }

        [HttpPost("api/reviews/update-status")]
        public async Task<IActionResult> UpdateReviewStatus([FromServices] ReviewBal reviewBal, [FromForm] long reviewId, [FromForm] string newStatus)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            var success = await reviewBal.UpdateReviewStatusAsync(reviewId, newStatus);
            return Json(new { success, message = success ? $"Review status changed to {newStatus}!" : "Failed to update review status." });
        }

        [HttpPost("api/reviews/delete")]
        public async Task<IActionResult> DeleteReview([FromServices] ReviewBal reviewBal, [FromForm] long reviewId)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            var success = await reviewBal.DeleteReviewAsync(reviewId);
            return Json(new { success, message = success ? "Review deleted successfully." : "Failed to delete review." });
        }
    }
}
