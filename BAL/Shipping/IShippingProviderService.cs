using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SAT1.BAL.Shipping
{
    public class ShipmentRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string RecipientStreet { get; set; } = string.Empty;
        public string RecipientCity { get; set; } = string.Empty;
        public string RecipientState { get; set; } = string.Empty;
        public string RecipientPostalCode { get; set; } = string.Empty;
        public string RecipientCountry { get; set; } = "United States";
        public string ItemDescription { get; set; } = "Fine Diamond Jewelry";
        public decimal DeclaredValueUSD { get; set; }
        public double WeightKg { get; set; } = 0.5;
    }

    public class ShipmentBookingResult
    {
        public bool Success { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string CarrierName { get; set; } = "DHL Express";
        public string TrackingUrl { get; set; } = string.Empty;
        public DateTime? EstimatedDeliveryDate { get; set; }
        public string InitialStatusNote { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class TrackingStatusResult
    {
        public bool Success { get; set; }
        public string InternalStatus { get; set; } = "InTransit";
        public string CarrierStatus { get; set; } = string.Empty;
        public string StatusNote { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime EventTimestamp { get; set; } = DateTime.Now;
        public DateTime? EstimatedDeliveryDate { get; set; }
        public string RawPayload { get; set; } = string.Empty;
    }

    public interface IShippingProviderService
    {
        string ProviderName { get; }
        Task<ShipmentBookingResult> BookShipmentAsync(ShipmentRequest request);
        Task<TrackingStatusResult> GetTrackingStatusAsync(string trackingNumber);
        bool VerifyWebhookSignature(HttpRequest request, string rawBody);
        TrackingStatusResult ParseWebhookPayload(string rawBody);
        string MapProviderStatusToInternalStatus(string providerStatus);
    }
}
