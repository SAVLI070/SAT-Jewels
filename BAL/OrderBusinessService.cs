using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SAT1.DAL;
using SAT1.Models;

namespace SAT1.BAL
{
    public class OrderBusinessService
    {
        private readonly OrderRepository _orderRepo;
        private readonly PayPalService _payPalService;
        private readonly RazorpayService _razorpayService;
        private readonly CatalogBal _catalogBal;

        public OrderBusinessService(
            OrderRepository orderRepo, 
            PayPalService payPalService, 
            RazorpayService razorpayService,
            CatalogBal catalogBal)
        {
            _orderRepo = orderRepo;
            _payPalService = payPalService;
            _razorpayService = razorpayService;
            _catalogBal = catalogBal;
        }

        // DTO for Shipping Details
        public class ShippingAddressDto
        {
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Street { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public string PostalCode { get; set; } = string.Empty;
            public string Country { get; set; } = "United States";
        }

        public class CartItemDto
        {
            public string ProductId { get; set; } = string.Empty;
            public string? Name { get; set; }
            public int Quantity { get; set; } = 1;
            public string? StoneType { get; set; }
            public string? Metal { get; set; }
            public string? Size { get; set; }
            public decimal PriceUSD { get; set; }
        }

        // 1. Create PayPal Order Flow (Server-Authoritative Pricing + Stone Type & Multi-Item Support)
        public async Task<(string payPalOrderId, string internalOrderId, decimal serverCalculatedPriceUSD, string approveUrl)> CreatePayPalOrderFlowAsync(
            string productId, 
            int quantity, 
            string userId, 
            string userEmail, 
            ShippingAddressDto shipping,
            bool includePhysicalCertificate = false,
            string? stoneType = null,
            List<CartItemDto>? cartItems = null)
        {
            decimal totalAmountUSD = 0m;
            var itemDescriptions = new List<string>();

            if (cartItems != null && cartItems.Count > 0)
            {
                foreach (var item in cartItems)
                {
                    if (string.IsNullOrWhiteSpace(item.ProductId)) continue;
                    var prod = await _orderRepo.GetProductByIdAsync(item.ProductId);
                    int qty = Math.Clamp(item.Quantity, 1, 10);
                    if (prod == null)
                    {
                        throw new InvalidOperationException($"SECURITY ALERT: Product '{item.ProductId}' not found in database.");
                    }
                    bool itemIsMoiss = string.Equals(item.StoneType, "Moissanite", StringComparison.OrdinalIgnoreCase);
                    decimal unitPrice = itemIsMoiss ? prod.MoissanitePrice : prod.PriceUSD;

                    if (!string.IsNullOrWhiteSpace(item.Metal) || !string.IsNullOrWhiteSpace(item.Size))
                    {
                        var (isValid, validatedPrice, _, _) = await _catalogBal.CalculateServerValidatedPriceAsync(item.ProductId, item.Metal, null, item.Size, item.StoneType);
                        if (isValid && validatedPrice > 0)
                        {
                            unitPrice = validatedPrice;
                        }
                    }
                    if (item.PriceUSD > 0 && (Math.Abs(item.PriceUSD - unitPrice) <= 0.01m || (item.PriceUSD >= unitPrice && item.PriceUSD - unitPrice <= 600m)))
                    {
                        unitPrice = item.PriceUSD;
                    }

                    totalAmountUSD += (unitPrice * qty);
                    var stoneLabel = itemIsMoiss ? "Moissanite" : "Lab Grown Diamond";
                    itemDescriptions.Add($"{item.Name ?? prod?.Name ?? "Fine Jewelry"} ({stoneLabel}, Qty: {qty})");
                }
            }

            if (totalAmountUSD == 0m)
            {
                int safeQty = Math.Clamp(quantity, 1, 10);
                var product = await _orderRepo.GetProductByIdAsync(productId);
                if (product == null)
                {
                    throw new Exception($"SECURITY ALERT: Product '{productId}' not found in database.");
                }

                bool isMoiss = string.Equals(stoneType, "Moissanite", StringComparison.OrdinalIgnoreCase);
                decimal unitPrice = isMoiss ? product.MoissanitePrice : product.PriceUSD;
                totalAmountUSD = unitPrice * safeQty;
                var stoneLabel = isMoiss ? "Moissanite" : "Lab Grown Diamond";
                itemDescriptions.Add($"{product.Name} ({stoneLabel}, Qty: {safeQty})");
            }

            decimal giaCertFee = includePhysicalCertificate ? 50.00m : 0.00m;
            totalAmountUSD = Math.Max(0.01m, totalAmountUSD + giaCertFee);

            var internalOrderId = "SAT-ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // Create PayPal Order on PayPal Servers
            var (payPalOrderId, approveUrl) = await _payPalService.CreateOrderAsync(totalAmountUSD, "USD", internalOrderId);

            string itemsSummary = string.Join(" + ", itemDescriptions);
            if (includePhysicalCertificate) itemsSummary += " [Includes Physical GIA Hardcopy Certificate]";
            if (itemsSummary.Length > 250) itemsSummary = itemsSummary.Substring(0, 247) + "...";

            // Save Pending Order Record in DB
            var pendingOrder = new Order
            {
                OrderId = internalOrderId,
                OrderNumber = $"SAT-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                UserId = userId,
                CustomerEmail = userEmail,
                ItemName = itemsSummary,
                ExpectedAmount = totalAmountUSD,
                TotalAmountUSD = totalAmountUSD,
                IncludesPhysicalGiaCert = includePhysicalCertificate,
                GiaCertFeeUSD = giaCertFee,
                Currency = "USD",
                PaymentProvider = "PayPal",
                ProviderOrderId = payPalOrderId,
                PayPalTransactionId = payPalOrderId,
                PaymentMethod = "PayPal Express USD",
                OrderStatus = "Pending",
                ShippingFullName = shipping.FullName,
                ShippingPhone = shipping.Phone,
                ShippingStreet = shipping.Street,
                ShippingCity = shipping.City,
                ShippingState = shipping.State,
                ShippingPostalCode = shipping.PostalCode,
                ShippingCountry = shipping.Country,
                CustomerRegion = shipping.Country,
                CreatedAt = DateTime.Now
            };

            await _orderRepo.CreatePendingOrderAsync(pendingOrder);

            return (payPalOrderId, internalOrderId, totalAmountUSD, approveUrl);
        }

        // 2. Verify and Confirm PayPal Payment Server-Side
        public async Task<(bool success, string message, Order? order)> ConfirmAndSavePayPalPaymentAsync(string payPalOrderId, string? userId)
        {
            var order = await _orderRepo.GetOrderByProviderOrderIdAsync(payPalOrderId);
            if (order == null)
            {
                return (false, "Order not found in database.", null);
            }

            // Call PayPal Server GET API directly (Never trust frontend status)
            var (status, capturedAmount, payerEmail, payerName) = await _payPalService.GetOrderDetailsAsync(payPalOrderId);

            if (!status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) && !status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                await _orderRepo.FlagSuspiciousPaymentAsync(payPalOrderId, $"UNVERIFIED STATUS: PayPal status returned '{status}'.");
                return (false, $"PayPal order status is '{status}', expected COMPLETED.", order);
            }

            // Amount Mismatch Protection: Compare captured amount vs expected DB amount
            if (Math.Abs(order.ExpectedAmount - capturedAmount) > 0.01m && capturedAmount > 0m)
            {
                await _orderRepo.FlagSuspiciousPaymentAsync(payPalOrderId, $"PRICE TAMPERING DETECTED: Expected ${order.ExpectedAmount:F2} USD, but captured ${capturedAmount:F2} USD.");
                return (false, "SECURITY ALERT: Captured payment amount does not match product price.", order);
            }

            var buyerInfo = $"Payer: {payerName} <{payerEmail}>";
            var (isPaid, wasAlreadyPaid, updatedOrder) = await _orderRepo.MarkOrderAsPaidIdempotentlyAsync(
                payPalOrderId, payPalOrderId, capturedAmount > 0 ? capturedAmount : order.ExpectedAmount, buyerInfo, "PayPal");

            if (!isPaid)
            {
                return (false, "Payment confirmation failed or flagged as suspicious.", updatedOrder);
            }

            return (true, wasAlreadyPaid ? "Order already verified and completed." : "PayPal payment verified and order marked as Paid!", updatedOrder);
        }

        // 3. Create Razorpay Order Flow (Server-Authoritative Pricing + Stone Type & Multi-Item Support)
        public async Task<(string razorpayOrderId, string internalOrderId, decimal serverCalculatedPriceUSD, string razorpayKeyId)> CreateRazorpayOrderFlowAsync(
            string productId, 
            int quantity, 
            string userId, 
            string userEmail, 
            ShippingAddressDto shipping,
            bool includePhysicalCertificate = false,
            string? stoneType = null,
            List<CartItemDto>? cartItems = null)
        {
            decimal totalAmountUSD = 0m;
            var itemDescriptions = new List<string>();

            if (cartItems != null && cartItems.Count > 0)
            {
                foreach (var item in cartItems)
                {
                    if (string.IsNullOrWhiteSpace(item.ProductId)) continue;
                    var prod = await _orderRepo.GetProductByIdAsync(item.ProductId);
                    int qty = Math.Clamp(item.Quantity, 1, 10);
                    if (prod == null)
                    {
                        throw new InvalidOperationException($"SECURITY ALERT: Product '{item.ProductId}' not found in database.");
                    }
                    bool itemIsMoiss = string.Equals(item.StoneType, "Moissanite", StringComparison.OrdinalIgnoreCase);
                    decimal unitPrice = itemIsMoiss ? prod.MoissanitePrice : prod.PriceUSD;

                    if (!string.IsNullOrWhiteSpace(item.Metal) || !string.IsNullOrWhiteSpace(item.Size))
                    {
                        var (isValid, validatedPrice, _, _) = await _catalogBal.CalculateServerValidatedPriceAsync(item.ProductId, item.Metal, null, item.Size, item.StoneType);
                        if (isValid && validatedPrice > 0)
                        {
                            unitPrice = validatedPrice;
                        }
                    }
                    if (item.PriceUSD > 0 && (Math.Abs(item.PriceUSD - unitPrice) <= 0.01m || (item.PriceUSD >= unitPrice && item.PriceUSD - unitPrice <= 600m)))
                    {
                        unitPrice = item.PriceUSD;
                    }

                    totalAmountUSD += (unitPrice * qty);
                    var stoneLabel = itemIsMoiss ? "Moissanite" : "Lab Grown Diamond";
                    itemDescriptions.Add($"{item.Name ?? prod?.Name ?? "Fine Jewelry"} ({stoneLabel}, Qty: {qty})");
                }
            }

            if (totalAmountUSD == 0m)
            {
                int safeQty = Math.Clamp(quantity, 1, 10);
                var product = await _orderRepo.GetProductByIdAsync(productId);
                if (product == null)
                {
                    throw new Exception($"SECURITY ALERT: Product '{productId}' not found in database.");
                }

                bool isMoiss = string.Equals(stoneType, "Moissanite", StringComparison.OrdinalIgnoreCase);
                decimal unitPrice = isMoiss ? product.MoissanitePrice : product.PriceUSD;
                totalAmountUSD = unitPrice * safeQty;
                var stoneLabel = isMoiss ? "Moissanite" : "Lab Grown Diamond";
                itemDescriptions.Add($"{product.Name} ({stoneLabel}, Qty: {safeQty})");
            }

            decimal giaCertFee = includePhysicalCertificate ? 50.00m : 0.00m;
            totalAmountUSD = Math.Max(0.01m, totalAmountUSD + giaCertFee);

            var internalOrderId = "SAT-ORD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // Create Razorpay Order via API (Amount calculated on Server)
            var (razorpayOrderId, amountUSD, currency) = await _razorpayService.CreateOrderAsync(totalAmountUSD, "USD", internalOrderId);

            string itemsSummary = string.Join(" + ", itemDescriptions);
            if (includePhysicalCertificate) itemsSummary += " [Includes Physical GIA Hardcopy Certificate]";
            if (itemsSummary.Length > 250) itemsSummary = itemsSummary.Substring(0, 247) + "...";

            // Save Pending Order Record in DB
            var pendingOrder = new Order
            {
                OrderId = internalOrderId,
                OrderNumber = $"SAT-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                UserId = userId,
                CustomerEmail = userEmail,
                ItemName = itemsSummary,
                ExpectedAmount = totalAmountUSD,
                TotalAmountUSD = totalAmountUSD,
                IncludesPhysicalGiaCert = includePhysicalCertificate,
                GiaCertFeeUSD = giaCertFee,
                Currency = "USD",
                PaymentProvider = "Razorpay",
                ProviderOrderId = razorpayOrderId,
                PaymentMethod = "Razorpay International Card USD",
                OrderStatus = "Pending",
                ShippingFullName = shipping.FullName,
                ShippingPhone = shipping.Phone,
                ShippingStreet = shipping.Street,
                ShippingCity = shipping.City,
                ShippingState = shipping.State,
                ShippingPostalCode = shipping.PostalCode,
                ShippingCountry = shipping.Country,
                CustomerRegion = shipping.Country,
                CreatedAt = DateTime.Now
            };

            await _orderRepo.CreatePendingOrderAsync(pendingOrder);

            return (razorpayOrderId, internalOrderId, totalAmountUSD, _razorpayService.KeyId);
        }

        // 4. Verify and Confirm Razorpay Payment (Signature + Server Amount Verification)
        public async Task<(bool success, string message, Order? order)> ConfirmAndSaveRazorpayPaymentAsync(
            string razorpayOrderId, 
            string razorpayPaymentId, 
            string razorpaySignature, 
            string? userId)
        {
            var order = await _orderRepo.GetOrderByProviderOrderIdAsync(razorpayOrderId);
            if (order == null)
            {
                return (false, "Order not found in database.", null);
            }

            // Step 1: Verify HMAC-SHA256 Signature Server-Side
            bool isValidSig = _razorpayService.VerifyPaymentSignature(razorpayOrderId, razorpayPaymentId, razorpaySignature);
            if (!isValidSig)
            {
                await _orderRepo.FlagSuspiciousPaymentAsync(razorpayOrderId, "INVALID RAZORPAY SIGNATURE: HMAC signature verification failed.");
                return (false, "SECURITY ALERT: Razorpay signature verification failed.", order);
            }

            // Step 2: Fetch Payment Details directly from Razorpay Server API
            var (status, capturedAmountUSD, email, contact) = await _razorpayService.GetPaymentDetailsAsync(razorpayPaymentId);

            if (!status.Equals("captured", StringComparison.OrdinalIgnoreCase) && !status.Equals("authorized", StringComparison.OrdinalIgnoreCase))
            {
                await _orderRepo.FlagSuspiciousPaymentAsync(razorpayOrderId, $"UNVERIFIED STATUS: Razorpay status returned '{status}'.");
                return (false, $"Razorpay payment status is '{status}', expected captured.", order);
            }

            // Step 3: Amount Mismatch Protection
            if (Math.Abs(order.ExpectedAmount - capturedAmountUSD) > 0.01m && capturedAmountUSD > 0m)
            {
                await _orderRepo.FlagSuspiciousPaymentAsync(razorpayOrderId, $"PRICE TAMPERING DETECTED: Expected ${order.ExpectedAmount:F2} USD, but captured ${capturedAmountUSD:F2} USD.");
                return (false, "SECURITY ALERT: Captured payment amount does not match product price.", order);
            }

            var buyerInfo = $"Razorpay Payer: {email} ({contact})";
            var (isPaid, wasAlreadyPaid, updatedOrder) = await _orderRepo.MarkOrderAsPaidIdempotentlyAsync(
                razorpayOrderId, razorpayPaymentId, capturedAmountUSD > 0 ? capturedAmountUSD : order.ExpectedAmount, buyerInfo, "Razorpay");

            if (!isPaid)
            {
                return (false, "Razorpay payment processing failed or flagged as suspicious.", updatedOrder);
            }

            return (true, wasAlreadyPaid ? "Order already verified and completed." : "Razorpay payment verified and order marked as Paid!", updatedOrder);
        }

        // 5. PayPal Webhook Processing (Idempotent Safety Net)
        public async Task<bool> ProcessPayPalWebhookAsync(string payloadJson, IHeaderDictionary headers)
        {
            var headerDict = headers.ToDictionary(h => h.Key.ToLower(), h => h.Value.ToString());
            bool isValidSignature = await _payPalService.VerifyWebhookSignatureAsync(payloadJson, headerDict);

            if (!isValidSignature)
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var eventType = doc.RootElement.GetProperty("event_type").GetString();

                if (string.Equals(eventType, "PAYMENT.CAPTURE.COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(eventType, "CHECKOUT.ORDER.APPROVED", StringComparison.OrdinalIgnoreCase))
                {
                    var resource = doc.RootElement.GetProperty("resource");
                    string orderId = resource.TryGetProperty("supplementary_data", out var supp) && supp.TryGetProperty("related_ids", out var rel) && rel.TryGetProperty("order_id", out var ordId)
                        ? ordId.GetString() ?? ""
                        : resource.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(orderId))
                    {
                        await ConfirmAndSavePayPalPaymentAsync(orderId, "Webhook_System");
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 6. Razorpay Webhook Processing (Idempotent Safety Net)
        public async Task<bool> ProcessRazorpayWebhookAsync(string payloadJson, string signatureHeader)
        {
            bool isValidSignature = _razorpayService.VerifyWebhookSignature(payloadJson, signatureHeader);
            if (!isValidSignature)
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var eventType = doc.RootElement.GetProperty("event").GetString();

                if (string.Equals(eventType, "payment.captured", StringComparison.OrdinalIgnoreCase))
                {
                    var paymentEntity = doc.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
                    var razorpayPaymentId = paymentEntity.GetProperty("id").GetString()!;
                    var razorpayOrderId = paymentEntity.GetProperty("order_id").GetString()!;
                    long amountSubunits = paymentEntity.GetProperty("amount").GetInt64();
                    decimal capturedAmountUSD = amountSubunits / 100m;

                    var order = await _orderRepo.GetOrderByProviderOrderIdAsync(razorpayOrderId);
                    if (order != null)
                    {
                        var buyerInfo = paymentEntity.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
                        await _orderRepo.MarkOrderAsPaidIdempotentlyAsync(razorpayOrderId, razorpayPaymentId, capturedAmountUSD, buyerInfo, "Razorpay");
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
