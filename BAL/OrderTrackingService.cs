using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SAT1.BAL.Shipping;
using SAT1.DAL;
using SAT1.Models;

namespace SAT1.BAL
{
    public class OrderTrackingService
    {
        private readonly OrderTrackingRepository _trackingRepo;
        private readonly IShippingProviderService _shippingProvider;
        private readonly EmailNotificationService _emailService;

        public OrderTrackingService(
            OrderTrackingRepository trackingRepo, 
            IShippingProviderService shippingProvider, 
            EmailNotificationService emailService)
        {
            _trackingRepo = trackingRepo;
            _shippingProvider = shippingProvider;
            _emailService = emailService;
        }

        // B1. Fully Automated Shipment Booking (Triggered by Payment Success)
        public async Task<(bool success, string trackingNumber, string message)> BookShipmentAsync(string orderId)
        {
            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            if (order == null)
            {
                return (false, "", $"Order '{orderId}' not found in database.");
            }

            // Check if already booked
            if (!string.IsNullOrWhiteSpace(order.TrackingNumber) && order.CurrentTrackingStatus != "OrderPlaced" && order.CurrentTrackingStatus != "Pending")
            {
                return (true, order.TrackingNumber, "Shipment already booked with carrier.");
            }

            var request = new ShipmentRequest
            {
                OrderId = order.OrderId,
                OrderNumber = order.OrderNumber,
                RecipientName = order.ShippingFullName,
                RecipientEmail = order.CustomerEmail,
                RecipientPhone = order.ShippingPhone,
                RecipientStreet = order.ShippingStreet,
                RecipientCity = order.ShippingCity,
                RecipientState = order.ShippingState,
                RecipientPostalCode = order.ShippingPostalCode,
                RecipientCountry = order.ShippingCountry,
                ItemDescription = order.ItemName,
                DeclaredValueUSD = order.TotalAmountUSD,
                WeightKg = 0.5
            };

            var result = await _shippingProvider.BookShipmentAsync(request);

            if (result.Success)
            {
                await _trackingRepo.UpdateOrderTrackingStatusAsync(
                    order.OrderId,
                    "ShipmentBooked",
                    result.TrackingNumber,
                    result.CarrierName,
                    result.TrackingUrl,
                    result.EstimatedDeliveryDate,
                    DateTime.UtcNow);

                var historyEntry = new OrderTrackingHistory
                {
                    OrderId = order.OrderId,
                    Status = "ShipmentBooked",
                    StatusNote = result.InitialStatusNote,
                    CarrierName = result.CarrierName,
                    TrackingNumber = result.TrackingNumber,
                    TrackingUrl = result.TrackingUrl,
                    Location = "Surat Diamond Hub, India",
                    Source = "System",
                    CreatedAt = DateTime.UtcNow
                };

                await _trackingRepo.AddTrackingHistoryAsync(historyEntry);

                // Send Automated Confirmation Email
                order.TrackingNumber = result.TrackingNumber;
                order.CarrierName = result.CarrierName;
                order.TrackingUrl = result.TrackingUrl;
                order.EstimatedDeliveryDate = result.EstimatedDeliveryDate;
                await _emailService.SendTrackingUpdateEmailAsync(order, "ShipmentBooked", result.InitialStatusNote, result.TrackingUrl);

                return (true, result.TrackingNumber, "Shipment booked automatically with carrier!");
            }
            else
            {
                // Record failure exception in tracking log for Admin Exception Monitor
                var exceptionEntry = new OrderTrackingHistory
                {
                    OrderId = order.OrderId,
                    Status = "Exception",
                    StatusNote = $"Shipment booking failed: {result.ErrorMessage}",
                    CarrierName = _shippingProvider.ProviderName,
                    Source = "System",
                    CreatedAt = DateTime.UtcNow
                };
                await _trackingRepo.AddTrackingHistoryAsync(exceptionEntry);

                return (false, "", result.ErrorMessage);
            }
        }

        // B2. Process Inbound Carrier Webhook Event (Idempotent)
        public async Task<(bool success, string message)> ProcessCarrierWebhookAsync(HttpRequest request, string rawBody)
        {
            if (!_shippingProvider.VerifyWebhookSignature(request, rawBody))
            {
                return (false, "Invalid carrier webhook signature.");
            }

            var parsed = _shippingProvider.ParseWebhookPayload(rawBody);
            if (!parsed.Success)
            {
                return (false, parsed.StatusNote);
            }

            // Extract tracking number or order ID from payload
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var trackingNo = root.TryGetProperty("tracking_number", out var tn) ? tn.GetString() : "";
            var orderId = root.TryGetProperty("order_id", out var oi) ? oi.GetString() : "";

            Order? order = null;
            if (!string.IsNullOrWhiteSpace(trackingNo))
            {
                order = await _trackingRepo.GetOrderByTrackingNumberAsync(trackingNo);
            }
            if (order == null && !string.IsNullOrWhiteSpace(orderId))
            {
                order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            }

            if (order == null)
            {
                return (false, "Matching order not found for carrier webhook.");
            }

            return await ProcessStatusUpdateAsync(order.OrderId, parsed, "System");
        }

        // Process status update (from webhook or polling job)
        public async Task<(bool success, string message)> ProcessStatusUpdateAsync(
            string orderId, 
            TrackingStatusResult statusResult, 
            string source = "System")
        {
            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            if (order == null) return (false, "Order not found");

            await _trackingRepo.UpdateOrderTrackingStatusAsync(
                order.OrderId,
                statusResult.InternalStatus,
                null,
                null,
                null,
                statusResult.EstimatedDeliveryDate,
                null);

            var entry = new OrderTrackingHistory
            {
                OrderId = order.OrderId,
                Status = statusResult.InternalStatus,
                StatusNote = statusResult.StatusNote,
                CarrierName = order.CarrierName,
                TrackingNumber = order.TrackingNumber,
                TrackingUrl = order.TrackingUrl,
                Location = statusResult.Location,
                Source = source,
                CreatedAt = statusResult.EventTimestamp
            };

            await _trackingRepo.AddTrackingHistoryAsync(entry);

            // Send notification to buyer
            await _emailService.SendTrackingUpdateEmailAsync(order, statusResult.InternalStatus, statusResult.StatusNote, order.TrackingUrl);

            return (true, $"Order tracking updated to {statusResult.InternalStatus}");
        }
    }
}
