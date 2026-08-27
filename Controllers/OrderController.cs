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
        public async Task<IActionResult> Track(string? orderId, string? email)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return View("TrackLookup");
            }

            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId.Trim());
            if (order == null)
            {
                ViewBag.ErrorMessage = $"Order '{orderId}' was not found. Please check your order reference number.";
                return View("TrackLookup");
            }

            ViewBag.History = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(order.OrderId);
            return View("Track", order);
        }
    }
}
