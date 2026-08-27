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
        public IActionResult Index()
        {
            if (!CheckAccess())
            {
                return HandleUnauthorized();
            }
            ViewBag.Title = "Dashboard Overview";
            return View("Index");
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
            var orders = await _adminBal.GetAllOrdersWithTrackingAsync(status, q);
            ViewBag.StatusFilter = status ?? "All";
            ViewBag.SearchQuery = q ?? "";
            return View(orders);
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
        public async Task<IActionResult> Reviews([FromServices] ReviewBal reviewBal, string? status)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Product Customer Reviews Moderation";
            var reviews = await reviewBal.GetAllReviewsAsync(status);
            ViewBag.StatusFilter = status ?? "All";
            return View(reviews);
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
