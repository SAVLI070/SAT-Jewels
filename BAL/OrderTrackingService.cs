using System;
using System.Threading.Tasks;
using SAT1.DAL;
using SAT1.Models;

namespace SAT1.BAL
{
    public class OrderTrackingService
    {
        private readonly OrderTrackingRepository _trackingRepo;
        private readonly EmailNotificationService _emailService;

        public OrderTrackingService(
            OrderTrackingRepository trackingRepo, 
            EmailNotificationService emailService)
        {
            _trackingRepo = trackingRepo;
            _emailService = emailService;
        }

        // 1. Update Order Status and Tracking Link (Admin / Direct Flow)
        public async Task<(bool success, string message)> UpdateOrderTrackingAsync(
            string orderId, 
            string status, 
            string? trackingUrl, 
            string? trackingNumber, 
            string? carrierName, 
            string? note = null)
        {
            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            if (order == null)
            {
                return (false, $"Order '{orderId}' not found in database.");
            }

            var cleanStatus = string.IsNullOrWhiteSpace(status) ? order.OrderStatus : status.Trim();
            var cleanTrackingUrl = !string.IsNullOrWhiteSpace(trackingUrl) ? trackingUrl.Trim() : order.TrackingUrl;
            var cleanTrackingNo = !string.IsNullOrWhiteSpace(trackingNumber) ? trackingNumber.Trim() : order.TrackingNumber;
            var cleanCarrier = !string.IsNullOrWhiteSpace(carrierName) ? carrierName.Trim() : (!string.IsNullOrWhiteSpace(order.CarrierName) ? order.CarrierName : "Direct Parcel Dispatch");
            var cleanNote = !string.IsNullOrWhiteSpace(note) ? note.Trim() : $"Order status updated to {cleanStatus}.";

            // Update order record
            await _trackingRepo.UpdateOrderTrackingStatusAsync(
                order.OrderId,
                cleanStatus,
                cleanTrackingNo,
                cleanCarrier,
                cleanTrackingUrl,
                null,
                DateTime.Now);

            // Add history milestone
            var historyEntry = new OrderTrackingHistory
            {
                OrderId = order.OrderId,
                Status = cleanStatus,
                StatusNote = cleanNote,
                CarrierName = cleanCarrier,
                TrackingNumber = cleanTrackingNo,
                TrackingUrl = cleanTrackingUrl,
                Location = "Surat Atelier, India",
                Source = "Admin",
                CreatedAt = DateTime.Now
            };

            await _trackingRepo.AddTrackingHistoryAsync(historyEntry);

            // Send automated email alert with tracking link if available
            try
            {
                order.OrderStatus = cleanStatus;
                order.CurrentTrackingStatus = cleanStatus;
                order.TrackingNumber = cleanTrackingNo;
                order.CarrierName = cleanCarrier;
                order.TrackingUrl = cleanTrackingUrl;
                await _emailService.SendTrackingUpdateEmailAsync(order, cleanStatus, cleanNote, cleanTrackingUrl);
            }
            catch
            {
                // Non-fatal if email fails
            }

            return (true, "Order tracking updated successfully.");
        }

        // Backward-compatible method called after payment (marks order as placed)
        public async Task<(bool success, string trackingNumber, string message)> BookShipmentAsync(string orderId)
        {
            var order = await _trackingRepo.GetOrderByOrderIdAsync(orderId);
            if (order == null)
            {
                return (false, "", $"Order '{orderId}' not found.");
            }

            var historyEntry = new OrderTrackingHistory
            {
                OrderId = order.OrderId,
                Status = "Order Placed",
                StatusNote = "Order received and confirmed. Jewelry artisan crafting scheduled at Surat atelier.",
                CarrierName = "Direct Parcel Dispatch",
                TrackingNumber = order.OrderNumber,
                TrackingUrl = "",
                Location = "Surat Diamond Hub, India",
                Source = "System",
                CreatedAt = DateTime.Now
            };

            await _trackingRepo.AddTrackingHistoryAsync(historyEntry);
            return (true, order.OrderNumber, "Order registered and confirmed.");
        }
    }
}
