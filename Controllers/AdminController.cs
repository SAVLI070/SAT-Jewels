using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.BAL;
using SAT1.Models;

namespace SAT1.Controllers
{
    [Route("admin")]
    [Microsoft.AspNetCore.Authorization.Authorize]
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

        [HttpGet("logout")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public IActionResult Logout()
        {
            return RedirectToAction("Logout", "Account");
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
        public async Task<IActionResult> Orders(string? status, string? q, string? userId, string? email, string? userName, int page = 1, int pageSize = 15)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Customer Orders & Live Tracking";
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;

            var counts = await _adminBal.GetOrderStatusCountsAsync(userId, email);
            
            ViewBag.TotalCount = counts.TotalCount;
            ViewBag.PendingCount = counts.PendingCount;
            ViewBag.PaidCount = counts.PaidCount;
            ViewBag.DispatchedCount = counts.DispatchedCount;
            ViewBag.InTransitCount = counts.InTransitCount;
            ViewBag.DeliveredCount = counts.DeliveredCount;

            var (filteredOrders, totalFiltered) = await _adminBal.GetOrdersPagedAsync(status, q, userId, email, page, pageSize);
            int totalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            ViewBag.StatusFilter = status ?? "All";
            ViewBag.SearchQuery = q ?? "";
            ViewBag.UserId = userId ?? "";
            ViewBag.UserEmail = email ?? "";
            ViewBag.UserName = userName ?? "";
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalFiltered = totalFiltered;

            return View(filteredOrders);
        }

        [HttpGet("users")]
        public async Task<IActionResult> Users(int page = 1, int pageSize = 15)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Customer Accounts & Directory";
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;

            var (users, totalCount) = await _adminBal.GetUsersPagedAsync(page, pageSize);
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalPages;

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
        public async Task<IActionResult> Reviews([FromServices] ReviewBal reviewBal, [FromServices] SatJewelDbContext db, string? status, int page = 1, int pageSize = 12)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Product Customer Reviews Moderation";
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            var allCount = await db.ProductReviews.CountAsync();
            var approvedCount = await db.ProductReviews.CountAsync(r => r.Status.ToLower() == "approved");
            var pendingCount = await db.ProductReviews.CountAsync(r => r.Status.ToLower() == "pending");
            var rejectedCount = await db.ProductReviews.CountAsync(r => r.Status.ToLower() == "rejected");

            ViewBag.AllReviewsCount = allCount;
            ViewBag.ApprovedCount = approvedCount;
            ViewBag.PendingCount = pendingCount;
            ViewBag.RejectedCount = rejectedCount;

            var (pagedReviews, totalCount) = await reviewBal.GetReviewsPagedAsync(status, page, pageSize);
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            ViewBag.StatusFilter = status ?? "All";
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalPages;

            return View(pagedReviews);
        }

        public class SaveTrackingRequest
        {
            public long OrderId { get; set; }
            public string? CourierName { get; set; }
            public string? TrackingNumber { get; set; }
            public string? TrackingUrl { get; set; }
            public bool SendEmail { get; set; } = true;
        }

        [HttpPost("orders/save-tracking")]
        public async Task<IActionResult> SaveTrackingInfo([FromBody] SaveTrackingRequest req, [FromServices] SatJewelDbContext db, [FromServices] EmailNotificationService emailService)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            if (req == null || req.OrderId <= 0) return BadRequest(new { success = false, message = "Invalid order ID." });

            var order = await db.Orders.FindAsync(req.OrderId);
            if (order == null) return NotFound(new { success = false, message = "Order not found." });

            order.CarrierName = !string.IsNullOrWhiteSpace(req.CourierName) ? req.CourierName.Trim() : "Courier";
            order.TrackingNumber = req.TrackingNumber?.Trim() ?? string.Empty;
            order.TrackingUrl = req.TrackingUrl?.Trim() ?? string.Empty;
            order.OrderStatus = "Shipped";
            order.CurrentTrackingStatus = "InTransit";

            if (req.SendEmail)
            {
                order.TrackingInfoSentAt = DateTime.Now;
                await emailService.SendOrderShippedEmailAsync(order, order.CarrierName, order.TrackingNumber, order.TrackingUrl);
            }

            await db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = req.SendEmail
                    ? "Tracking saved & shipped notification email sent to customer!"
                    : "Tracking information saved successfully!",
                sentAt = order.TrackingInfoSentAt?.ToString("MMM dd, yyyy hh:mm tt")
            });
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
