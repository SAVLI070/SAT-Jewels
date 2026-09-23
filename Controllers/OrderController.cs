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

            if (!string.IsNullOrWhiteSpace(search))
            {
                var order = await _trackingRepo.GetOrderByOrderIdAsync(search) 
                         ?? await _trackingRepo.GetOrderByTrackingNumberAsync(search);

                if (order != null && !string.IsNullOrWhiteSpace(order.TrackingUrl))
                {
                    if (Uri.TryCreate(order.TrackingUrl, UriKind.Absolute, out var uri) && 
                        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                    {
                        return Redirect(order.TrackingUrl);
                    }
                }
            }

            return RedirectToAction("Orders", "Account");
        }
    }
}
