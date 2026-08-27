using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.DAL
{
    public class OrderTrackingRepository
    {
        private readonly SatJewelDbContext _context;

        public OrderTrackingRepository(SatJewelDbContext context)
        {
            _context = context;
        }

        public async Task<Order?> GetOrderByOrderIdAsync(string orderId)
        {
            return await _context.Orders
                .Include(o => o.TrackingHistory)
                .FirstOrDefaultAsync(o => o.OrderId == orderId || o.OrderNumber == orderId);
        }

        public async Task<Order?> GetOrderByTrackingNumberAsync(string trackingNumber)
        {
            return await _context.Orders
                .Include(o => o.TrackingHistory)
                .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber);
        }

        public async Task<List<OrderTrackingHistory>> GetTrackingHistoryByOrderIdAsync(string orderId)
        {
            return await _context.OrderTrackingHistory
                .Where(h => h.OrderId == orderId)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> AddTrackingHistoryAsync(OrderTrackingHistory entry)
        {
            try
            {
                // Idempotency check: don't insert exact duplicate events for the same order and status
                var exists = await _context.OrderTrackingHistory.AnyAsync(h => 
                    h.OrderId == entry.OrderId && 
                    h.Status == entry.Status && 
                    h.StatusNote == entry.StatusNote);

                if (exists) return true;

                _context.OrderTrackingHistory.Add(entry);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderTrackingRepository.AddTrackingHistoryAsync Error]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateOrderTrackingStatusAsync(
            string orderId, 
            string status, 
            string? trackingNumber = null, 
            string? carrierName = null, 
            string? trackingUrl = null, 
            DateTime? estDelivery = null, 
            DateTime? bookedAt = null)
        {
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order == null) return false;

                order.CurrentTrackingStatus = status;
                if (!string.IsNullOrWhiteSpace(trackingNumber)) order.TrackingNumber = trackingNumber;
                if (!string.IsNullOrWhiteSpace(carrierName)) order.CarrierName = carrierName;
                if (!string.IsNullOrWhiteSpace(trackingUrl)) order.TrackingUrl = trackingUrl;
                if (estDelivery.HasValue) order.EstimatedDeliveryDate = estDelivery.Value;
                if (bookedAt.HasValue) order.ShipmentBookedAt = bookedAt.Value;

                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderTrackingRepository.UpdateOrderTrackingStatusAsync Error]: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Order>> GetActiveOrdersForPollingAsync()
        {
            var terminalStatuses = new[] { "Delivered", "Cancelled", "Returned", "Refunded" };
            return await _context.Orders
                .Where(o => o.OrderStatus == "Paid" && 
                            !string.IsNullOrWhiteSpace(o.TrackingNumber) && 
                            !terminalStatuses.Contains(o.CurrentTrackingStatus))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<OrderTrackingHistory>> GetShippingExceptionsAsync()
        {
            return await _context.OrderTrackingHistory
                .Include(h => h.Order)
                .Where(h => h.Status == "Exception" || h.StatusNote.Contains("Failed") || h.StatusNote.Contains("Delay"))
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
        }
    }
}
