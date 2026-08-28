using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SAT1.DAL;

namespace SAT1.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderTrackingRepository _trackingRepo;

        public OrderController(OrderTrackingRepository trackingRepo)
        {
            _trackingRepo = trackingRepo;
        }

        [HttpGet]
        public async Task<IActionResult> Track(string? orderId, string? email, string? query)
        {
            var search = !string.IsNullOrWhiteSpace(query) ? query.Trim() : 
                         !string.IsNullOrWhiteSpace(orderId) ? orderId.Trim() : 
                         !string.IsNullOrWhiteSpace(email) ? email.Trim() : "";

            if (string.IsNullOrWhiteSpace(search))
            {
                return View("TrackLookup");
            }

            // 1. If search is an Email (contains @)
            if (search.Contains("@"))
            {
                var emailOrders = await _trackingRepo.GetOrdersByEmailAsync(search);
                if (emailOrders.Count == 0)
                {
                    ViewBag.ErrorMessage = $"No orders found associated with email '{search}'. Please ensure you entered the exact email used during checkout.";
                    ViewBag.SearchQuery = search;
                    return View("TrackLookup");
                }

                if (emailOrders.Count == 1)
                {
                    var singleOrder = emailOrders[0];
                    ViewBag.History = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(singleOrder.OrderId);
                    return View("Track", singleOrder);
                }

                // Multiple orders found for this email: Show selection table
                ViewBag.MatchedOrders = emailOrders;
                ViewBag.SearchEmail = search;
                return View("TrackLookup");
            }

            // 2. Direct Order ID or Tracking Number Search
            var order = await _trackingRepo.GetOrderByOrderIdAsync(search) 
                     ?? await _trackingRepo.GetOrderByTrackingNumberAsync(search);

            if (order != null)
            {
                ViewBag.History = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(order.OrderId);
                return View("Track", order);
            }

            // 3. Fallback fuzzy search by query
            var fuzzyOrders = await _trackingRepo.GetOrdersByQueryAsync(search);
            if (fuzzyOrders.Count == 1)
            {
                var single = fuzzyOrders[0];
                ViewBag.History = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(single.OrderId);
                return View("Track", single);
            }
            else if (fuzzyOrders.Count > 1)
            {
                ViewBag.MatchedOrders = fuzzyOrders;
                ViewBag.SearchQuery = search;
                return View("TrackLookup");
            }

            ViewBag.ErrorMessage = $"No orders found matching '{search}'. Please check your Order ID (e.g. SAT-ORD-12345) or registered email address.";
            ViewBag.SearchQuery = search;
            return View("TrackLookup");
        }
    }
}
