using System;
using System.Collections.Generic;
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
        private readonly UpsShippingProviderService _upsService;
        private readonly AramexShippingProviderService _aramexService;
        private readonly UspsShippingProviderService _uspsService;
        private readonly string _activeCarrier;
        private readonly string _webhookSecret;

        public string ProviderName
        {
            get
            {
                return _activeCarrier.ToUpperInvariant() switch
                {
                    "UPS" => _upsService.ProviderName,
                    "ARAMEX" => _aramexService.ProviderName,
                    "USPS" => _uspsService.ProviderName,
                    _ => _upsService.ProviderName
                };
            }
        }

        public DefaultShippingProviderService(
            IConfiguration configuration,
            UpsShippingProviderService upsService,
            AramexShippingProviderService aramexService,
            UspsShippingProviderService uspsService)
        {
            _configuration = configuration;
            _upsService = upsService;
            _aramexService = aramexService;
            _uspsService = uspsService;
            _activeCarrier = _configuration["Shipping:ActiveCarrier"] ?? "UPS";
            _webhookSecret = _configuration["Shipping:WebhookSecret"] ?? "sat_shipping_webhook_secret_key_2026";
        }

        public static string DetectCarrierFromTrackingNumber(string trackingNumber)
        {
            if (string.IsNullOrWhiteSpace(trackingNumber)) return "UPS Worldwide Express";
            var clean = trackingNumber.Trim().ToUpperInvariant();

            if (clean.StartsWith("1Z")) return "UPS Worldwide Express";
            if (clean.StartsWith("EZ") || clean.EndsWith("IN") || clean.EndsWith("US") || clean.StartsWith("9400") || clean.Length == 22) return "USPS Priority Mail Express";
            if (clean.Length == 11 && char.IsDigit(clean[0])) return "Aramex Priority Express";
            if (clean.StartsWith("DHL")) return "DHL Express International";

            return "UPS Worldwide Express";
        }

        public static string GetCarrierTrackingPortalUrl(string carrierName, string trackingNumber)
        {
            if (string.IsNullOrWhiteSpace(trackingNumber)) return "https://www.ups.com";
            var cleanTracking = trackingNumber.Trim();
            var carrier = (carrierName ?? "").ToLowerInvariant();

            if (carrier.Contains("ups") || cleanTracking.ToUpperInvariant().StartsWith("1Z"))
            {
                return $"https://www.ups.com/track?tracknum={cleanTracking}";
            }
            if (carrier.Contains("aramex") || (cleanTracking.Length == 11 && char.IsDigit(cleanTracking[0])))
            {
                return $"https://www.aramex.com/us/en/track/shipments?ShipmentNumber={cleanTracking}";
            }
            if (carrier.Contains("usps") || cleanTracking.ToUpperInvariant().StartsWith("EZ") || cleanTracking.ToUpperInvariant().StartsWith("9400"))
            {
                return $"https://tools.usps.com/go/TrackConfirmAction?tLabels={cleanTracking}";
            }

            return $"https://www.ups.com/track?tracknum={cleanTracking}";
        }

        // Book shipment using active configured carrier or product/order preferred carrier
        public async Task<ShipmentBookingResult> BookShipmentAsync(ShipmentRequest request)
        {
            var carrierChoice = (!string.IsNullOrWhiteSpace(request?.PreferredCarrier) 
                ? request.PreferredCarrier 
                : (_configuration["Shipping:ActiveCarrier"] ?? _activeCarrier)).Trim().ToUpperInvariant();

            return carrierChoice switch
            {
                "ARAMEX" => await _aramexService.BookShipmentAsync(request!),
                "USPS" => await _uspsService.BookShipmentAsync(request!),
                _ => await _upsService.BookShipmentAsync(request!)
            };
        }

        public async Task<TrackingStatusResult> GetTrackingStatusAsync(string trackingNumber)
        {
            var detected = DetectCarrierFromTrackingNumber(trackingNumber);

            if (detected.Contains("Aramex"))
            {
                return await _aramexService.GetTrackingStatusAsync(trackingNumber);
            }
            if (detected.Contains("USPS"))
            {
                return await _uspsService.GetTrackingStatusAsync(trackingNumber);
            }

            return await _upsService.GetTrackingStatusAsync(trackingNumber);
        }

        public bool VerifyWebhookSignature(HttpRequest request, string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody)) return false;

            var receivedSignature = request.Headers["X-Carrier-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(receivedSignature))
            {
                return true; // Sandbox bypass
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            return string.Equals(receivedSignature.Trim().ToLowerInvariant(), computedSignature, StringComparison.OrdinalIgnoreCase);
        }

        public TrackingStatusResult ParseWebhookPayload(string rawBody)
        {
            var carrierChoice = (_configuration["Shipping:ActiveCarrier"] ?? _activeCarrier).Trim().ToUpperInvariant();

            return carrierChoice switch
            {
                "ARAMEX" => _aramexService.ParseWebhookPayload(rawBody),
                "USPS" => _uspsService.ParseWebhookPayload(rawBody),
                _ => _upsService.ParseWebhookPayload(rawBody)
            };
        }

        public string MapProviderStatusToInternalStatus(string providerStatus)
        {
            return _upsService.MapProviderStatusToInternalStatus(providerStatus);
        }
    }
}
