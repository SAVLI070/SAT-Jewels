using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAT1.BAL;
using SAT1.Models;
using Stripe;
using Stripe.Checkout;

namespace SAT1.Controllers
{
    public class PaymentController : Controller
    {
        private readonly CatalogBal _catalogBal;
        private readonly SatJewelDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            CatalogBal catalogBal,
            SatJewelDbContext db,
            IConfiguration config,
            ILogger<PaymentController> logger)
        {
            _catalogBal = catalogBal;
            _db = db;
            _config = config;
            _logger = logger;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutCartRequest request)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Sign in required to checkout.",
                    loginUrl = "/Account/SignIn?returnUrl=/Product/Cart"
                });
            }

            var secretKey = _config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith("sk_test_REPLACE", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Stripe is not configured. Add Stripe:SecretKey via user secrets or appsettings."
                });
            }

            if (request?.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { success = false, message = "Your cart is empty." });
            }

            StripeConfiguration.ApiKey = secretKey;

            var lineItems = new List<SessionLineItemOptions>();
            var validatedLines = new List<object>();
            var firstItemName = "";
            decimal orderTotal = 0;

            foreach (var line in request.Items)
            {
                if (string.IsNullOrWhiteSpace(line.Id))
                {
                    return BadRequest(new { success = false, message = "Invalid cart line: missing product id." });
                }

                var qty = line.Quantity < 1 ? 1 : Math.Min(line.Quantity, 25);
                var (isValid, unitPrice, itemName, errorMsg) =
                    await _catalogBal.CalculateServerValidatedPriceAsync(line.Id, line.Metal, line.Carat);

                if (!isValid)
                {
                    return BadRequest(new { success = false, message = errorMsg });
                }

                if (string.IsNullOrEmpty(firstItemName))
                {
                    firstItemName = itemName;
                }

                var unitAmountCents = (long)Math.Round(unitPrice * 100m, MidpointRounding.AwayFromZero);
                if (unitAmountCents < 50)
                {
                    return BadRequest(new { success = false, message = $"Price for '{itemName}' is below Stripe minimum." });
                }

                var descriptionParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(line.Metal)) descriptionParts.Add(line.Metal);
                if (!string.IsNullOrWhiteSpace(line.Carat)) descriptionParts.Add(line.Carat);
                if (!string.IsNullOrWhiteSpace(line.Engraving)) descriptionParts.Add($"Engraving: {line.Engraving}");

                lineItems.Add(new SessionLineItemOptions
                {
                    Quantity = qty,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = unitAmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = itemName,
                            Description = descriptionParts.Count > 0
                                ? string.Join(" | ", descriptionParts)
                                : "SAT Jewel GIA-certified piece"
                        }
                    }
                });

                orderTotal += unitPrice * qty;
                validatedLines.Add(new
                {
                    id = line.Id,
                    name = itemName,
                    metal = line.Metal,
                    carat = line.Carat,
                    engraving = line.Engraving,
                    quantity = qty,
                    unitPriceUSD = unitPrice
                });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var order = new Order
            {
                OrderId = Guid.NewGuid().ToString("N"),
                ItemName = validatedLines.Count == 1
                    ? firstItemName
                    : $"{validatedLines.Count} vault items",
                Amount = orderTotal,
                Currency = "USD",
                CustomerRegion = "Global",
                PaymentMethod = "Stripe Checkout",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                CustomerEmail = email,
                ItemsJson = JsonSerializer.Serialize(validatedLines)
            };

            try
            {
                var sessionOptions = new SessionCreateOptions
                {
                    Mode = "payment",
                    SuccessUrl = $"{baseUrl}/Payment/Success?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{baseUrl}/Payment/Cancel",
                    CustomerEmail = string.IsNullOrWhiteSpace(email) ? null : email,
                    LineItems = lineItems,
                    Metadata = new Dictionary<string, string>
                    {
                        ["orderId"] = order.OrderId,
                        ["userId"] = userId
                    },
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            ["orderId"] = order.OrderId,
                            ["userId"] = userId
                        }
                    }
                };

                var sessionService = new SessionService();
                var session = await sessionService.CreateAsync(sessionOptions);

                order.StripeSessionId = session.Id;
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                return Ok(new { success = true, url = session.Url, orderId = order.OrderId });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe session creation failed");
                return BadRequest(new { success = false, message = $"Stripe error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout session failed");
                return BadRequest(new { success = false, message = "Unable to start checkout. Please try again." });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Success(string? session_id)
        {
            if (string.IsNullOrWhiteSpace(session_id))
            {
                ViewBag.Message = "Missing Stripe session.";
                return View();
            }

            var secretKey = _config["Stripe:SecretKey"];
            if (!string.IsNullOrWhiteSpace(secretKey) && !secretKey.StartsWith("sk_test_REPLACE", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    StripeConfiguration.ApiKey = secretKey;
                    var sessionService = new SessionService();
                    var session = await sessionService.GetAsync(session_id);

                    if (session.PaymentStatus == "paid")
                    {
                        await MarkOrderPaidAsync(session);
                    }

                    ViewBag.OrderId = session.Metadata != null && session.Metadata.TryGetValue("orderId", out var oid)
                        ? oid
                        : "";
                    ViewBag.AmountTotal = (session.AmountTotal ?? 0) / 100m;
                    ViewBag.CustomerEmail = session.CustomerDetails?.Email ?? session.CustomerEmail ?? "";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve Stripe session on Success");
                }
            }

            ViewBag.SessionId = session_id;
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Cancel()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var webhookSecret = _config["Stripe:WebhookSecret"];

            try
            {
                Event stripeEvent;
                if (!string.IsNullOrWhiteSpace(webhookSecret) && !webhookSecret.StartsWith("whsec_REPLACE", StringComparison.OrdinalIgnoreCase))
                {
                    var signature = Request.Headers["Stripe-Signature"];
                    stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
                }
                else
                {
                    // Dev fallback when webhook secret is not set (not for production)
                    stripeEvent = EventUtility.ParseEvent(json);
                }

                if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session != null)
                    {
                        await MarkOrderPaidAsync(session);
                    }
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook verification failed");
                return BadRequest();
            }
        }

        private async Task MarkOrderPaidAsync(Session session)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.StripeSessionId == session.Id);

            if (order == null && session.Metadata != null && session.Metadata.TryGetValue("orderId", out var orderId))
            {
                order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            }

            if (order == null)
            {
                _logger.LogWarning("No order found for Stripe session {SessionId}", session.Id);
                return;
            }

            if (string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            order.Status = "Paid";
            order.StripeSessionId = session.Id;
            order.StripePaymentIntentId = session.PaymentIntentId ?? order.StripePaymentIntentId;
            if (!string.IsNullOrWhiteSpace(session.CustomerDetails?.Email))
            {
                order.CustomerEmail = session.CustomerDetails.Email;
            }
            else if (!string.IsNullOrWhiteSpace(session.CustomerEmail))
            {
                order.CustomerEmail = session.CustomerEmail;
            }

            await _db.SaveChangesAsync();
        }
    }
}
