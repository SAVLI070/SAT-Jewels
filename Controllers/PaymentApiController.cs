using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentApiController : ControllerBase
    {
        private readonly SatJewelDbContext _context;
        private readonly IConfiguration _configuration;

        public PaymentApiController(SatJewelDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: /api/paymentapi/config (Public Endpoint for Frontend PayPal SDK Client ID)
        [HttpGet("config")]
        public IActionResult GetPayPalConfig()
        {
            var clientId = _configuration["PayPal:ClientId"] ?? "sb";
            var mode = _configuration["PayPal:Mode"] ?? "Sandbox";
            return Ok(new { clientId, mode, currency = "USD" });
        }

        // POST: /api/paymentapi/process-paypal
        [HttpPost("process-paypal")]
        public async Task<IActionResult> ProcessPayPalOrder([FromBody] PayPalCheckoutRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.PayPalOrderId))
            {
                return BadRequest(new { success = false, message = "Invalid PayPal order transaction payload." });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? req.PayerEmail ?? "client@satjewels.com";

                var itemName = !string.IsNullOrWhiteSpace(req.ItemName) ? req.ItemName : "Bespoke Jewelry Order";
                var amount = req.Amount > 0 ? req.Amount : 1700.00m;

                var order = new Order
                {
                    OrderId = "SAT-ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    UserId = userId,
                    CustomerEmail = userEmail,
                    ItemName = itemName,
                    Amount = amount,
                    Currency = "USD",
                    CustomerRegion = !string.IsNullOrWhiteSpace(req.PayerCountryCode) ? req.PayerCountryCode : "United States",
                    PaymentMethod = "PayPal Express USD (" + req.PayPalOrderId + ")",
                    PayPalTransactionId = req.PayPalOrderId,
                    Status = "Completed (Insured GIA Dispatch)",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    orderId = order.OrderId,
                    transactionId = req.PayPalOrderId,
                    amount = order.Amount,
                    currency = order.Currency,
                    message = "PayPal transaction captured successfully and order logged to database."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Database order processing error: " + ex.Message });
            }
        }
    }

    public class PayPalCheckoutRequest
    {
        public string PayPalOrderId { get; set; } = string.Empty;
        public string? ItemName { get; set; }
        public decimal Amount { get; set; }
        public string? PayerEmail { get; set; }
        public string? PayerCountryCode { get; set; }
    }
}
