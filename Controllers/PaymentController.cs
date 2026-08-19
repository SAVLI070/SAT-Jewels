using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAT1.BAL;

namespace SAT1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly OrderBusinessService _orderBusinessService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(OrderBusinessService orderBusinessService, ILogger<PaymentController> logger)
        {
            _orderBusinessService = orderBusinessService;
            _logger = logger;
        }

        // Request DTOs
        public class CreateOrderRequest
        {
            public string ProductId { get; set; } = string.Empty;
            public int Quantity { get; set; } = 1;

            // Shipping Details
            public string ShippingFullName { get; set; } = string.Empty;
            public string ShippingPhone { get; set; } = string.Empty;
            public string ShippingStreet { get; set; } = string.Empty;
            public string ShippingCity { get; set; } = string.Empty;
            public string ShippingState { get; set; } = string.Empty;
            public string ShippingPostalCode { get; set; } = string.Empty;
            public string ShippingCountry { get; set; } = "United States";
        }

        public class VerifyPayPalRequest
        {
            public string PayPalOrderId { get; set; } = string.Empty;
        }

        public class VerifyRazorpayRequest
        {
            public string RazorpayOrderId { get; set; } = string.Empty;
            public string RazorpayPaymentId { get; set; } = string.Empty;
            public string RazorpaySignature { get; set; } = string.Empty;
        }

        private OrderBusinessService.ShippingAddressDto MapShipping(CreateOrderRequest req)
        {
            return new OrderBusinessService.ShippingAddressDto
            {
                FullName = string.IsNullOrWhiteSpace(req.ShippingFullName) ? "Valued Client" : req.ShippingFullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(req.ShippingPhone) ? "+1-800-555-0199" : req.ShippingPhone.Trim(),
                Street = string.IsNullOrWhiteSpace(req.ShippingStreet) ? "5th Avenue Luxury Suite" : req.ShippingStreet.Trim(),
                City = string.IsNullOrWhiteSpace(req.ShippingCity) ? "New York" : req.ShippingCity.Trim(),
                State = string.IsNullOrWhiteSpace(req.ShippingState) ? "NY" : req.ShippingState.Trim(),
                PostalCode = string.IsNullOrWhiteSpace(req.ShippingPostalCode) ? "10001" : req.ShippingPostalCode.Trim(),
                Country = string.IsNullOrWhiteSpace(req.ShippingCountry) ? "United States" : req.ShippingCountry.Trim()
            };
        }

        // 1. POST /api/payment/create-order (PayPal Order Creation - Amount Calculated Server-Side)
        [HttpPost("create-order")]
        [AllowAnonymous]
        public async Task<IActionResult> CreatePayPalOrder([FromBody] CreateOrderRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ProductId))
            {
                return BadRequest(new { success = false, message = "ProductId is required." });
            }

            try
            {
                var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest" : "guest";
                var userEmail = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.Email) ?? "client@satjewels.com" : "client@satjewels.com";

                var shipping = MapShipping(req);
                var (payPalOrderId, internalOrderId, serverCalculatedPriceUSD, approveUrl) = await _orderBusinessService.CreatePayPalOrderFlowAsync(
                    req.ProductId, req.Quantity, userId, userEmail, shipping);

                return Ok(new
                {
                    success = true,
                    orderId = payPalOrderId, // Return PayPal Order ID for Smart Buttons / Hosted Fields
                    internalOrderId = internalOrderId,
                    amountUSD = serverCalculatedPriceUSD,
                    currency = "USD",
                    approveUrl = approveUrl,
                    message = "PayPal order initialized with server-authoritative price."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayPal Create Order Error");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 2. POST /api/payment/verify (PayPal Server-Side Verification)
        [HttpPost("verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyPayPalPayment([FromBody] VerifyPayPalRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.PayPalOrderId))
            {
                return BadRequest(new { success = false, message = "PayPalOrderId is required." });
            }

            try
            {
                var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
                var (success, message, order) = await _orderBusinessService.ConfirmAndSavePayPalPaymentAsync(req.PayPalOrderId, userId);

                if (!success)
                {
                    return BadRequest(new { success = false, message = message, orderId = order?.OrderId });
                }

                return Ok(new
                {
                    success = true,
                    message = message,
                    orderId = order?.OrderId,
                    amountPaid = order?.AmountPaid,
                    currency = order?.Currency ?? "USD",
                    status = order?.OrderStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayPal Verification Error");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 3. POST /api/payment/webhook (PayPal Webhook Receiver)
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayPalWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var payloadJson = await reader.ReadToEndAsync();

                bool verified = await _orderBusinessService.ProcessPayPalWebhookAsync(payloadJson, Request.Headers);
                if (!verified)
                {
                    _logger.LogWarning("UNVERIFIED PAYPAL WEBHOOK RECEIVED");
                    return Unauthorized(new { message = "Invalid PayPal Webhook Signature" });
                }

                return Ok(new { status = "received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayPal Webhook Processing Exception");
                return Ok(new { status = "error_handled" }); // Return 200 to prevent webhook retry spam
            }
        }

        // 4. POST /api/payment/razorpay/create-order (Razorpay Order Creation - Amount Calculated Server-Side)
        [HttpPost("razorpay/create-order")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateRazorpayOrder([FromBody] CreateOrderRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ProductId))
            {
                return BadRequest(new { success = false, message = "ProductId is required." });
            }

            try
            {
                var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest" : "guest";
                var userEmail = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.Email) ?? "client@satjewels.com" : "client@satjewels.com";

                var shipping = MapShipping(req);
                var (razorpayOrderId, internalOrderId, serverCalculatedPriceUSD, razorpayKeyId) = await _orderBusinessService.CreateRazorpayOrderFlowAsync(
                    req.ProductId, req.Quantity, userId, userEmail, shipping);

                return Ok(new
                {
                    success = true,
                    razorpayOrderId = razorpayOrderId,
                    internalOrderId = internalOrderId,
                    amountUSD = serverCalculatedPriceUSD,
                    currency = "USD",
                    keyId = razorpayKeyId,
                    message = "Razorpay order initialized with server-authoritative price."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Razorpay Create Order Error");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 5. POST /api/payment/razorpay/verify (Razorpay Signature Verification)
        [HttpPost("razorpay/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyRazorpayPayment([FromBody] VerifyRazorpayRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.RazorpayOrderId) || string.IsNullOrWhiteSpace(req.RazorpayPaymentId) || string.IsNullOrWhiteSpace(req.RazorpaySignature))
            {
                return BadRequest(new { success = false, message = "RazorpayOrderId, RazorpayPaymentId, and RazorpaySignature are required." });
            }

            try
            {
                var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
                var (success, message, order) = await _orderBusinessService.ConfirmAndSaveRazorpayPaymentAsync(
                    req.RazorpayOrderId, req.RazorpayPaymentId, req.RazorpaySignature, userId);

                if (!success)
                {
                    return BadRequest(new { success = false, message = message, orderId = order?.OrderId });
                }

                return Ok(new
                {
                    success = true,
                    message = message,
                    orderId = order?.OrderId,
                    amountPaid = order?.AmountPaid,
                    currency = order?.Currency ?? "USD",
                    status = order?.OrderStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Razorpay Verification Error");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 6. POST /api/payment/razorpay/webhook (Razorpay Webhook Receiver)
        [HttpPost("razorpay/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> RazorpayWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var payloadJson = await reader.ReadToEndAsync();

                var signatureHeader = Request.Headers["X-Razorpay-Signature"].ToString();
                bool verified = await _orderBusinessService.ProcessRazorpayWebhookAsync(payloadJson, signatureHeader);

                if (!verified)
                {
                    _logger.LogWarning("UNVERIFIED RAZORPAY WEBHOOK RECEIVED");
                    return Unauthorized(new { message = "Invalid Razorpay Webhook Signature" });
                }

                return Ok(new { status = "received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Razorpay Webhook Processing Exception");
                return Ok(new { status = "error_handled" });
            }
        }
    }
}
