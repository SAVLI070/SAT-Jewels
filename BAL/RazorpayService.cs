using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SAT1.BAL
{
    public class RazorpayService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public RazorpayService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public string KeyId => _config["Razorpay:KeyId"] ?? "rzp_test_YOUR_KEY_ID";
        public string KeySecret => _config["Razorpay:KeySecret"] ?? "YOUR_RAZORPAY_SECRET";
        public string WebhookSecret => _config["Razorpay:WebhookSecret"] ?? "YOUR_WEBHOOK_SECRET";
        private string BaseUrl => "https://api.razorpay.com/v1";

        // 1. Create Razorpay Order via REST API /v1/orders (Server Calculates Price)
        public async Task<(string razorpayOrderId, decimal amountUSD, string currency)> CreateOrderAsync(decimal amountUSD, string currency = "USD", string? receiptId = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/orders");
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KeyId}:{KeySecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            // Razorpay amount is in smallest currency subunit (cents for USD: $100.00 = 10000 cents)
            long amountSubunits = (long)Math.Round(amountUSD * 100m);

            var payload = new
            {
                amount = amountSubunits,
                currency = currency.ToUpper(),
                receipt = receiptId ?? $"rcpt_{Guid.NewGuid():N}",
                notes = new
                {
                    business = "SAT Luxury Jewellery",
                    export_market = "USA",
                    security = "Server-Authoritative Pricing"
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Razorpay Create Order Error: {response.StatusCode} - {responseJson}");
            }

            using var doc = JsonDocument.Parse(responseJson);
            var razorpayOrderId = doc.RootElement.GetProperty("id").GetString()!;

            return (razorpayOrderId, amountUSD, currency);
        }

        // 2. Verify Razorpay Payment Signature (HMAC-SHA256 of order_id + "|" + payment_id)
        public bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            if (string.IsNullOrWhiteSpace(razorpayOrderId) ||
                string.IsNullOrWhiteSpace(razorpayPaymentId) ||
                string.IsNullOrWhiteSpace(razorpaySignature))
            {
                return false;
            }

            var text = $"{razorpayOrderId}|{razorpayPaymentId}";
            var secretBytes = Encoding.UTF8.GetBytes(KeySecret);
            var textBytes = Encoding.UTF8.GetBytes(text);

            using var hmac = new HMACSHA256(secretBytes);
            var hashBytes = hmac.ComputeHash(textBytes);
            var generatedSignature = Convert.ToHexString(hashBytes).ToLower();

            return string.Equals(generatedSignature, razorpaySignature.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // 3. Verify Razorpay Webhook Signature (HMAC-SHA256 of payload)
        public bool VerifyWebhookSignature(string payloadJson, string signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(payloadJson) || string.IsNullOrWhiteSpace(signatureHeader))
            {
                return false;
            }

            var secretBytes = Encoding.UTF8.GetBytes(WebhookSecret);
            var textBytes = Encoding.UTF8.GetBytes(payloadJson);

            using var hmac = new HMACSHA256(secretBytes);
            var hashBytes = hmac.ComputeHash(textBytes);
            var generatedSignature = Convert.ToHexString(hashBytes).ToLower();

            return string.Equals(generatedSignature, signatureHeader.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // 4. Fetch Payment Details directly from Razorpay Server API (For extra verification)
        public async Task<(string status, decimal amountUSD, string email, string contact)> GetPaymentDetailsAsync(string razorpayPaymentId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/payments/{razorpayPaymentId}");
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KeyId}:{KeySecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Razorpay Fetch Payment Failed: {response.StatusCode} - {responseJson}");
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var status = root.GetProperty("status").GetString() ?? "unknown";
            long amountSubunits = root.GetProperty("amount").GetInt64();
            decimal amountUSD = amountSubunits / 100m;

            string email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
            string contact = root.TryGetProperty("contact", out var contactProp) ? contactProp.GetString() ?? "" : "";

            return (status, amountUSD, email, contact);
        }
    }
}
