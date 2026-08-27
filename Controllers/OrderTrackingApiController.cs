using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;
using SAT1.DAL;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api")]
    public class OrderTrackingApiController : ControllerBase
    {
        private readonly OrderTrackingRepository _trackingRepo;
        private readonly OrderTrackingService _orderTrackingService;

        public OrderTrackingApiController(OrderTrackingRepository trackingRepo, OrderTrackingService orderTrackingService)
        {
            _trackingRepo = trackingRepo;
            _orderTrackingService = orderTrackingService;
        }

        // 1. Authenticated / General Tracking Query by OrderId or OrderNumber
        [HttpGet("orders/{orderId}/tracking")]
        public async Task<IActionResult> GetTracking(string orderId)
        {
            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            if (order == null)
            {
                return NotFound(new { success = false, message = "Order not found." });
            }

            var history = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(order.OrderId);

            return Ok(new
            {
                success = true,
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                currentStatus = order.CurrentTrackingStatus,
                carrierName = order.CarrierName,
                trackingNumber = order.TrackingNumber,
                trackingUrl = order.TrackingUrl,
                estimatedDeliveryDate = order.EstimatedDeliveryDate?.ToString("yyyy-MM-dd"),
                history
            });
        }

        // 2. Secure Guest Tracking by OrderId + Email
        [HttpGet("orders/track")]
        public async Task<IActionResult> TrackGuestOrder([FromQuery] string orderId, [FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { success = false, message = "Order Number and Email are required." });
            }

            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId.Trim());
            if (order == null || !order.CustomerEmail.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { success = false, message = "No matching order found for this email." });
            }

            var history = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(order.OrderId);

            return Ok(new
            {
                success = true,
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                customerName = order.ShippingFullName,
                currentStatus = order.CurrentTrackingStatus,
                carrierName = order.CarrierName,
                trackingNumber = order.TrackingNumber,
                trackingUrl = order.TrackingUrl,
                estimatedDeliveryDate = order.EstimatedDeliveryDate?.ToString("MMM dd, yyyy"),
                shippingAddress = $"{order.ShippingCity}, {order.ShippingState}, {order.ShippingCountry}",
                history
            });
        }

        // 3. Automated Carrier Webhook Receiver (DHL / FedEx / Shiprocket)
        [HttpPost("tracking/webhook/{provider}")]
        public async Task<IActionResult> CarrierWebhook(string provider)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync();

            var (success, message) = await _orderTrackingService.ProcessCarrierWebhookAsync(Request, rawBody);
            if (!success)
            {
                return BadRequest(new { success = false, error = message });
            }

            return Ok(new { success = true, message });
        }
    }
}
