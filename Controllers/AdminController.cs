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
        private readonly CatalogBal _catalogBal;

        public AdminController(AdminBal adminBal, CatalogBal catalogBal)
        {
            _adminBal = adminBal;
            _catalogBal = catalogBal;
        }

        private bool CheckAccess()
        {
            return _adminBal.CheckAdminAccess(User);
        }

        [HttpPost("items/{id}/toggle-visibility")]
        [HttpPost("products/{id}/toggle-visibility")]
        public async Task<IActionResult> ToggleProductVisibility(string id, [FromQuery] bool active)
        {
            if (!CheckAccess())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var success = await _catalogBal.ToggleProductVisibilityAsync(id, active);
            if (!success) return NotFound(new { success = false, message = "Product not found" });

            return Ok(new { success = true, message = $"Product visibility updated to {(active ? "Visible" : "Hidden")}" });
        }

        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteCatalogItem(string id)
        {
            if (!CheckAccess())
            {
                return StatusCode(403, new { success = false, message = "Access Denied: Admin authorization required." });
            }

            var success = await _catalogBal.DeleteCatalogItemAsync(id);
            if (!success) return NotFound(new { success = false, message = "Catalog item not found" });

            return Ok(new { success = true, message = "Catalog item deleted successfully." });
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
            public string? OrderId { get; set; }
            public string? CourierName { get; set; }
            public string? TrackingNumber { get; set; }
            public string? TrackingUrl { get; set; }
            public string? TrackingStatus { get; set; }
            public string? StatusNote { get; set; }
            public bool SendEmail { get; set; } = true;
        }

        [HttpPost("orders/save-tracking")]
        public async Task<IActionResult> SaveTrackingInfo([FromBody] SaveTrackingRequest req, [FromServices] SatJewelDbContext db, [FromServices] EmailNotificationService emailService)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            if (req == null || string.IsNullOrWhiteSpace(req.OrderId)) return BadRequest(new { success = false, message = "Invalid order ID." });

            var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == req.OrderId || o.OrderNumber == req.OrderId);
            if (order == null) return NotFound(new { success = false, message = "Order not found." });

            var status = !string.IsNullOrWhiteSpace(req.TrackingStatus) ? req.TrackingStatus.Trim() : "InTransit";
            order.CarrierName = !string.IsNullOrWhiteSpace(req.CourierName) ? req.CourierName.Trim() : "DHL Express";
            order.TrackingNumber = req.TrackingNumber?.Trim() ?? string.Empty;
            order.TrackingUrl = req.TrackingUrl?.Trim() ?? string.Empty;
            order.CurrentTrackingStatus = status;

            if (status == "Delivered")
            {
                order.OrderStatus = "Delivered";
            }
            else if (status == "OrderPlaced")
            {
                order.OrderStatus = "Paid";
            }
            else
            {
                order.OrderStatus = "Shipped";
            }

            try
            {
                var history = new OrderTrackingHistory
                {
                    OrderId = order.OrderId,
                    Status = status,
                    CarrierName = order.CarrierName,
                    TrackingNumber = order.TrackingNumber,
                    TrackingUrl = order.TrackingUrl,
                    StatusNote = !string.IsNullOrWhiteSpace(req.StatusNote) ? req.StatusNote.Trim() : $"Admin updated stage to {status}",
                    Source = "Admin",
                    CreatedAt = DateTime.UtcNow
                };
                db.OrderTrackingHistory.Add(history);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderTrackingHistory Error]: {ex.Message}");
            }

            if (req.SendEmail)
            {
                order.TrackingInfoSentAt = DateTime.UtcNow;
                await emailService.SendTrackingStatusUpdateEmailAsync(order, order.CurrentTrackingStatus, order.CarrierName, order.TrackingNumber, order.TrackingUrl);
            }

            await db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = req.SendEmail
                    ? $"Status updated to '{status}' & notification email sent to customer!"
                    : $"Status updated to '{status}' and tracking saved successfully!",
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

        [HttpGet("customrequests")]
        [HttpGet("custom-requests")]
        [HttpGet("customrings")]
        public async Task<IActionResult> CustomRequests([FromServices] SatJewelDbContext db, string? status, int page = 1, int pageSize = 15)
        {
            if (!CheckAccess()) return HandleUnauthorized();
            ViewBag.Title = "Bespoke Custom Jewelry Inquiries";
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;

            var query = db.CustomRingInquiries.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == status);
            }

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(x => x.CreatedAt)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            ViewBag.StatusFilter = status ?? "All";

            return View(items);
        }

        [HttpPost("api/custom-requests/update-status")]
        public async Task<IActionResult> UpdateCustomRequestStatus([FromServices] SatJewelDbContext db, [FromForm] int id, [FromForm] string status)
        {
            if (!CheckAccess()) return Unauthorized(new { success = false, message = "Admin privileges required." });
            var inquiry = await db.CustomRingInquiries.FindAsync(id);
            if (inquiry == null) return NotFound(new { success = false, message = "Inquiry not found." });

            inquiry.Status = status;
            await db.SaveChangesAsync();
            return Json(new { success = true, message = $"Inquiry #{id} status updated to {status}." });
        }

        [HttpGet("Admin/ShippingExceptions")]
        public IActionResult ShippingExceptions()
        {
            return RedirectToAction("Orders");
        }
    }
}
