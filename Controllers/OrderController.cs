using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SAT1.DAL;

namespace SAT1.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderTrackingRepository _trackingRepo;
        private readonly OrderRepository _orderRepo;

        public OrderController(OrderTrackingRepository trackingRepo, OrderRepository orderRepo)
        {
            _trackingRepo = trackingRepo;
            _orderRepo = orderRepo;
        }

        [HttpGet]
        public async Task<IActionResult> Track(string? orderId, string? email, string? query)
        {
            var search = !string.IsNullOrWhiteSpace(query) ? query.Trim() : 
                         !string.IsNullOrWhiteSpace(orderId) ? orderId.Trim() : "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                var order = await _trackingRepo.GetOrderByOrderIdAsync(search) 
                         ?? await _trackingRepo.GetOrderByTrackingNumberAsync(search);

                if (order != null)
                {
                    // IDOR Protection: Verify caller is authenticated owner or supplied matching billing/shipping email
                    bool isAuthorized = false;
                    if (User.Identity?.IsAuthenticated == true)
                    {
                        var authUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var authEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                        if (userRole == "Admin" || 
                            (!string.IsNullOrEmpty(authUserId) && order.UserId == authUserId) ||
                            (!string.IsNullOrEmpty(authEmail) && order.CustomerEmail.Equals(authEmail, System.StringComparison.OrdinalIgnoreCase)))
                        {
                            isAuthorized = true;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(email) && order.CustomerEmail.Equals(email.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        isAuthorized = true;
                    }

                    if (isAuthorized && !string.IsNullOrWhiteSpace(order.TrackingUrl))
                    {
                        if (System.Uri.TryCreate(order.TrackingUrl, System.UriKind.Absolute, out var uri) && 
                            (uri.Scheme == System.Uri.UriSchemeHttps || uri.Scheme == System.Uri.UriSchemeHttp))
                        {
                            return Redirect(order.TrackingUrl);
                        }
                    }
                }
            }

            return RedirectToAction("Orders", "Account");
        }

        // Luxury Order Confirmation / Receipt Page
        [HttpGet]
        public async Task<IActionResult> Confirmation(string? orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return RedirectToAction("MyAccount", "Account");
            }

            var order = await _orderRepo.GetOrderByProviderOrderIdAsync(orderId);
            if (order == null)
            {
                order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            }

            if (order == null)
            {
                return RedirectToAction("MyAccount", "Account");
            }

            // IDOR Protection: Verify caller is owner or admin; otherwise mask customer PII
            bool isAuthorized = false;
            if (User.Identity?.IsAuthenticated == true)
            {
                var authUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var authEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                if (userRole == "Admin" ||
                    (!string.IsNullOrEmpty(authUserId) && order.UserId == authUserId) ||
                    (!string.IsNullOrEmpty(authEmail) && order.CustomerEmail.Equals(authEmail, System.StringComparison.OrdinalIgnoreCase)))
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                // Mask sensitive customer PII to prevent data harvesting via guessed Order IDs
                order.ShippingStreet = "Confidential Delivery Address";
                if (!string.IsNullOrWhiteSpace(order.ShippingPhone) && order.ShippingPhone.Length >= 4)
                {
                    order.ShippingPhone = "***-***-" + order.ShippingPhone.Substring(order.ShippingPhone.Length - 4);
                }
                if (!string.IsNullOrEmpty(order.CustomerEmail) && order.CustomerEmail.Contains('@'))
                {
                    var parts = order.CustomerEmail.Split('@');
                    order.CustomerEmail = (parts[0].Length > 2 ? parts[0].Substring(0, 2) : "*") + "***@" + parts[1];
                }
            }

            return View(order);
        }
    }
}
