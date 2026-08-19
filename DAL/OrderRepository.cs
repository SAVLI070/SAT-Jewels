using Microsoft.EntityFrameworkCore;
using SAT1.Models;

namespace SAT1.DAL
{
    public class OrderRepository
    {
        private readonly SatJewelDbContext _context;

        public OrderRepository(SatJewelDbContext context)
        {
            _context = context;
        }

        // 1. Get Product by ID (Server-authoritative price lookup)
        public async Task<CatalogItem?> GetProductByIdAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return null;

            // Check CatalogItems table
            var catalogItem = await _context.CatalogItems.FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
            if (catalogItem != null) return catalogItem;

            // Check Products table by numeric ProductId (e.g., sat-prod-101 -> 101)
            var cleanId = productId.Replace("sat-prod-", "").Replace("sat-local-", "");
            if (long.TryParse(cleanId, out long numericId))
            {
                var prod = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.ProductId == numericId && p.IsAvailable);
                if (prod != null)
                {
                    return new CatalogItem
                    {
                        Id = $"sat-prod-{prod.ProductId}",
                        Name = prod.ProductName,
                        CategoryId = prod.CategoryId.ToString(),
                        Spec = $"{prod.DefaultMetalType} | {prod.DefaultCaratWeight}ct",
                        PriceUSD = prod.BasePriceUSD,
                        ImageUrl = prod.Images.FirstOrDefault(i => i.IsMainImage)?.ImageUrl ?? "/assets/ring_1.jpg",
                        IsActive = prod.IsAvailable
                    };
                }
            }

            return null;
        }

        // 2. Create Pending Order Record
        public async Task<Order> CreatePendingOrderAsync(Order order)
        {
            order.OrderStatus = "Pending";
            order.CreatedAt = DateTime.UtcNow;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        // 3. Get Order by Internal OrderId or Provider Order ID
        public async Task<Order?> GetOrderByProviderOrderIdAsync(string providerOrderId)
        {
            if (string.IsNullOrWhiteSpace(providerOrderId)) return null;

            return await _context.Orders
                .FirstOrDefaultAsync(o => o.ProviderOrderId == providerOrderId || o.OrderId == providerOrderId || o.PayPalTransactionId == providerOrderId);
        }

        // 4. Mark Order As Paid Idempotently
        public async Task<(bool success, bool wasAlreadyPaid, Order? order)> MarkOrderAsPaidIdempotentlyAsync(
            string providerOrderId, 
            string providerTransactionId, 
            decimal amountPaid, 
            string buyerInfo, 
            string provider)
        {
            var order = await GetOrderByProviderOrderIdAsync(providerOrderId);
            if (order == null)
            {
                return (false, false, null);
            }

            // Idempotency Check: If already marked as Paid/Completed, return early without duplicate updates
            if (order.OrderStatus == "Paid" || order.OrderStatus.StartsWith("Completed"))
            {
                return (true, true, order);
            }

            // Verify amount mismatch protection
            if (Math.Abs(order.ExpectedAmount - amountPaid) > 0.01m)
            {
                order.IsSuspicious = true;
                order.SuspiciousReason = $"AMOUNT MISMATCH: Expected ${order.ExpectedAmount:F2} USD, but captured ${amountPaid:F2} USD via {provider}.";
                order.OrderStatus = "Flagged_Suspicious";
                await _context.SaveChangesAsync();
                return (false, false, order);
            }

            order.OrderStatus = "Completed (Insured GIA Home Delivery Dispatch)";
            order.AmountPaid = amountPaid;
            order.PaidAt = DateTime.UtcNow;
            order.ProviderPaymentId = providerTransactionId;
            order.PayPalTransactionId = providerTransactionId;
            order.PaymentProvider = provider;
            order.BuyerInfo = buyerInfo;
            order.IsSuspicious = false;

            // Log Payment transaction audit record
            var paymentAudit = new Payment
            {
                OrderId = order.OrderId,
                PaymentGateway = provider,
                GatewayTransactionId = providerTransactionId,
                ProviderOrderId = providerOrderId,
                Currency = order.Currency ?? "USD",
                AmountUSD = amountPaid,
                PaymentStatus = "Captured",
                SignatureVerified = true,
                PaymentDate = DateTime.UtcNow
            };

            _context.Payments.Add(paymentAudit);
            await _context.SaveChangesAsync();

            return (true, false, order);
        }

        // 5. Flag Suspicious Payment
        public async Task<bool> FlagSuspiciousPaymentAsync(string providerOrderId, string reason)
        {
            var order = await GetOrderByProviderOrderIdAsync(providerOrderId);
            if (order == null) return false;

            order.IsSuspicious = true;
            order.SuspiciousReason = reason;
            order.OrderStatus = "Flagged_Suspicious";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
