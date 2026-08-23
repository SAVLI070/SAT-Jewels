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
            // GATE 1: User Must Be Authenticated to Perform Payment
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { success = false, message = "SIGN IN REQUIRED: You must be signed in to your SAT Jewel account to complete payment." });
            }

            if (req == null || string.IsNullOrWhiteSpace(req.PayPalOrderId))
            {
                return BadRequest(new { success = false, message = "Invalid PayPal order transaction payload." });
            }

            // GATE 2: User Must Have Provided Complete Shipping Address before Payment
            if (string.IsNullOrWhiteSpace(req.ShippingFullName) ||
                string.IsNullOrWhiteSpace(req.ShippingPhone) ||
                string.IsNullOrWhiteSpace(req.ShippingStreet) ||
                string.IsNullOrWhiteSpace(req.ShippingCity) ||
                string.IsNullOrWhiteSpace(req.ShippingState) ||
                string.IsNullOrWhiteSpace(req.ShippingPostalCode) ||
                string.IsNullOrWhiteSpace(req.ShippingCountry))
            {
                return BadRequest(new { success = false, message = "SHIPPING ADDRESS REQUIRED: Complete home delivery shipping address (Full Name, Phone, Street, City, State, Postal Code, Country) is required before payment permission is granted." });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest";
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
                    CustomerRegion = req.ShippingCountry,
                    
                    // High Priority Home Delivery Shipping Address Mapping
                    ShippingFullName = req.ShippingFullName.Trim(),
                    ShippingPhone = req.ShippingPhone.Trim(),
                    ShippingStreet = req.ShippingStreet.Trim(),
                    ShippingCity = req.ShippingCity.Trim(),
                    ShippingState = req.ShippingState.Trim(),
                    ShippingPostalCode = req.ShippingPostalCode.Trim(),
                    ShippingCountry = req.ShippingCountry.Trim(),

                    PaymentMethod = "PayPal Express USD (" + req.PayPalOrderId + ")",
                    PayPalTransactionId = req.PayPalOrderId,
                    Status = "Completed (Insured GIA Home Delivery Dispatch)",
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
                    shippingAddress = $"{order.ShippingStreet}, {order.ShippingCity}, {order.ShippingState} {order.ShippingPostalCode}, {order.ShippingCountry}",
                    message = "PayPal transaction captured successfully and home delivery order registered!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Database order processing error: " + ex.Message });
            }
        }

        // POST: /api/paymentapi/process-paypal-me
        [HttpPost("process-paypal-me")]
        public async Task<IActionResult> ProcessPayPalMeOrder([FromBody] PayPalMeCheckoutRequest req)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { success = false, message = "SIGN IN REQUIRED: You must be signed in to your SAT Jewel account to complete payment." });
            }

            if (req == null)
            {
                return BadRequest(new { success = false, message = "Invalid PayPal.Me order payload." });
            }

            if (string.IsNullOrWhiteSpace(req.ShippingFullName) ||
                string.IsNullOrWhiteSpace(req.ShippingPhone) ||
                string.IsNullOrWhiteSpace(req.ShippingStreet) ||
                string.IsNullOrWhiteSpace(req.ShippingCity) ||
                string.IsNullOrWhiteSpace(req.ShippingState) ||
                string.IsNullOrWhiteSpace(req.ShippingPostalCode) ||
                string.IsNullOrWhiteSpace(req.ShippingCountry))
            {
                return BadRequest(new { success = false, message = "SHIPPING ADDRESS REQUIRED: Complete home delivery shipping address is required before payment initialization." });
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest";
                var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? req.PayerEmail ?? "client@satjewels.com";
                var itemName = !string.IsNullOrWhiteSpace(req.ItemName) ? req.ItemName : "Bespoke Jewelry Order";
                var amount = req.Amount > 0 ? req.Amount : 1700.00m;

                var orderId = "SAT-ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var formattedAmountStr = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var basePayPalMeUrl = _configuration["PayPal:PayPalMeUrl"] ?? "https://www.paypal.com/paypalme/satjewels";
                var directPaymentUrl = $"{basePayPalMeUrl.TrimEnd('/')}/{formattedAmountStr}USD";

                var order = new Order
                {
                    OrderId = orderId,
                    UserId = userId,
                    CustomerEmail = userEmail,
                    ItemName = itemName,
                    Amount = amount,
                    Currency = "USD",
                    CustomerRegion = req.ShippingCountry,
                    ShippingFullName = req.ShippingFullName.Trim(),
                    ShippingPhone = req.ShippingPhone.Trim(),
                    ShippingStreet = req.ShippingStreet.Trim(),
                    ShippingCity = req.ShippingCity.Trim(),
                    ShippingState = req.ShippingState.Trim(),
                    ShippingPostalCode = req.ShippingPostalCode.Trim(),
                    ShippingCountry = req.ShippingCountry.Trim(),
                    PaymentMethod = "PayPal.Me Direct Transfer (" + (req.PayPalTransactionId ?? "Pending Verification") + ")",
                    PayPalTransactionId = req.PayPalTransactionId ?? "PPME-" + orderId,
                    Status = "Payment Pending Verification (PayPal Direct Transfer)",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    orderId = order.OrderId,
                    directPaymentUrl = directPaymentUrl,
                    amount = order.Amount,
                    currency = order.Currency,
                    message = "Order registered! Redirecting to secure PayPal.Me USD payment page."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error registering order: " + ex.Message });
            }
        }
    }

    public class PayPalMeCheckoutRequest : PayPalCheckoutRequest
    {
        public string? PayPalTransactionId { get; set; }
    }

    public class PayPalCheckoutRequest
    {
        public string PayPalOrderId { get; set; } = string.Empty;
        public string? ItemName { get; set; }
        public decimal Amount { get; set; }
        public string? PayerEmail { get; set; }
        public string? PayerCountryCode { get; set; }

        // Home Delivery Address Payload
        public string? ShippingFullName { get; set; }
        public string? ShippingPhone { get; set; }
        public string? ShippingStreet { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingState { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingCountry { get; set; }
    }
}
