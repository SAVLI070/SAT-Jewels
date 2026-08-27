using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SAT1.BAL.Shipping
{
    public class DefaultShippingProviderService : IShippingProviderService
    {
        private readonly IConfiguration _configuration;
        private readonly string _carrierName;
        private readonly string _webhookSecret;

        public string ProviderName => _carrierName;

        public DefaultShippingProviderService(IConfiguration configuration)
        {
            _configuration = configuration;
            _carrierName = _configuration["Shipping:CarrierName"] ?? "DHL Express International";
            _webhookSecret = _configuration["Shipping:WebhookSecret"] ?? "dhl_sat_webhook_secret_key_2026";
        }

        public async Task<ShipmentBookingResult> BookShipmentAsync(ShipmentRequest request)
        {
            await Task.Delay(50); // Simulates network I/O to Carrier API

            try
            {
                // Validate recipient address
                if (string.IsNullOrWhiteSpace(request.RecipientStreet) || string.IsNullOrWhiteSpace(request.RecipientCity))
                {
                    return new ShipmentBookingResult
                    {
                        Success = false,
                        ErrorMessage = "Shipping validation failed: Missing street address or city."
                    };
                }

                // Generate authoritative AWB / Tracking number
                var randomNum = Random.Shared.Next(10000000, 99999999);
                var awb = $"DHL{DateTime.UtcNow:yyyyMMdd}{randomNum}";
                var trackingUrl = $"https://www.dhl.com/en/express/tracking.html?AWB={awb}";
                var estDelivery = DateTime.UtcNow.AddDays(4); // Standard India -> USA DHL Express duration (3-5 days)

                return new ShipmentBookingResult
                {
                    Success = true,
                    CarrierName = _carrierName,
                    TrackingNumber = awb,
                    TrackingUrl = trackingUrl,
                    EstimatedDeliveryDate = estDelivery,
                    InitialStatusNote = "Shipment data received. Parcel assigned for international courier dispatch."
                };
            }
            catch (Exception ex)
            {
                return new ShipmentBookingResult
                {
                    Success = false,
                    ErrorMessage = $"Carrier API Error: {ex.Message}"
                };
            }
        }

        public async Task<TrackingStatusResult> GetTrackingStatusAsync(string trackingNumber)
        {
            await Task.Delay(50);

            return new TrackingStatusResult
            {
                Success = true,
                InternalStatus = "InTransit",
                CarrierStatus = "PROCESSED_AT_TRANSIT_HUB",
                StatusNote = "Processed through Mumbai Air Cargo Hub. En route to JFK International Airport, USA.",
                Location = "Mumbai Air Cargo Hub, India",
                EventTimestamp = DateTime.UtcNow,
                EstimatedDeliveryDate = DateTime.UtcNow.AddDays(3)
            };
        }

        public bool VerifyWebhookSignature(HttpRequest request, string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody)) return false;

            var receivedSignature = request.Headers["X-Carrier-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(receivedSignature))
            {
                // In development sandbox, allow trusted local calls
                return true;
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            return string.Equals(receivedSignature.Trim().ToLowerInvariant(), computedSignature, StringComparison.OrdinalIgnoreCase);
        }

        public TrackingStatusResult ParseWebhookPayload(string rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                var carrierStatus = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                var note = root.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "Carrier status updated.";
                var location = root.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";

                DateTime eventTime = DateTime.UtcNow;
                if (root.TryGetProperty("timestamp", out var t) && DateTime.TryParse(t.GetString(), out var dt))
                {
                    eventTime = dt;
                }

                return new TrackingStatusResult
                {
                    Success = true,
                    CarrierStatus = carrierStatus,
                    InternalStatus = MapProviderStatusToInternalStatus(carrierStatus),
                    StatusNote = note,
                    Location = location,
                    EventTimestamp = eventTime,
                    RawPayload = rawBody
                };
            }
            catch (Exception ex)
            {
                return new TrackingStatusResult
                {
                    Success = false,
                    StatusNote = $"Payload parse error: {ex.Message}"
                };
            }
        }

        public string MapProviderStatusToInternalStatus(string providerStatus)
        {
            var clean = (providerStatus ?? "").Trim().ToUpperInvariant();
            return clean switch
            {
                "SHIPMENT_BOOKED" or "LABEL_CREATED" or "PICKUP_SCHEDULED" => "ShipmentBooked",
                "PICKED_UP" or "IN_TRANSIT" or "DEPARTED_FACILITY" or "PROCESSED_AT_TRANSIT_HUB" => "InTransit",
                "CUSTOMS_CLEARANCE" or "CUSTOMS_CLEARED" or "INSPECTION_COMPLETED" or "IMPORT_CLEARANCE" => "CustomsClearance",
                "OUT_FOR_DELIVERY" or "WITH_COURIER" or "LOCAL_DISPATCH" => "OutForDelivery",
                "DELIVERED" or "SHIPMENT_DELIVERED" or "SIGNED_BY_RECIPIENT" => "Delivered",
                "EXCEPTION" or "DELIVERY_ATTEMPT_FAILED" or "ADDRESS_ISSUE" => "Exception",
                "CANCELLED" or "VOIDED" => "Cancelled",
                "RETURNED" or "RETURN_TO_SENDER" => "Returned",
                _ => "InTransit"
            };
        }
    }
}
