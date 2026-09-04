using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        [Required(ErrorMessage = "Order ID is required.")]
        public string OrderId { get; set; } = Guid.NewGuid().ToString();

        [MaxLength(50)]
        public string OrderNumber { get; set; } = $"SAT-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer Email is required.")]
        [EmailAddress(ErrorMessage = "Customer Email must be a valid email address.")]
        [MaxLength(255)]
        public string CustomerEmail { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public byte[] CustomerNameEncrypted { get; set; } = Array.Empty<byte>();

        public byte[] ShippingAddressEncrypted { get; set; } = Array.Empty<byte>();

        public string ShippingFullName { get; set; } = string.Empty;

        public string ShippingPhone { get; set; } = string.Empty;

        public string ShippingStreet { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required.")]
        [MaxLength(100)]
        public string ShippingCity { get; set; } = string.Empty;

        [Required(ErrorMessage = "State is required.")]
        [MaxLength(50)]
        public string ShippingState { get; set; } = string.Empty;

        [Required(ErrorMessage = "Postal Code is required.")]
        [MaxLength(20)]
        public string ShippingPostalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Country is required.")]
        [MaxLength(50)]
        public string ShippingCountry { get; set; } = "United States";

        public string CustomerRegion { get; set; } = "United States";

        [Required(ErrorMessage = "Total Amount in USD is required.")]
        [Column(TypeName = "numeric(18,2)")]
        public decimal TotalAmountUSD { get; set; }

        [NotMapped]
        public decimal Amount { get => TotalAmountUSD; set => TotalAmountUSD = value; }

        public string Currency { get; set; } = "USD";

        public string PaymentMethod { get; set; } = "PayPal Express USD";

        public string PayPalTransactionId { get; set; } = string.Empty;

        [MaxLength(50)]
        public string OrderStatus { get; set; } = "Pending";

        [NotMapped]
        public string Status { get => OrderStatus; set => OrderStatus = value; }

        public string PaymentProvider { get; set; } = "PayPal";

        [MaxLength(100)]
        public string ProviderOrderId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ProviderPaymentId { get; set; } = string.Empty;

        [Column(TypeName = "numeric(18,2)")]
        public decimal ExpectedAmount { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal AmountPaid { get; set; }

        public DateTime? PaidAt { get; set; }

        public string BuyerInfo { get; set; } = string.Empty;

        public bool IsSuspicious { get; set; } = false;

        public string? SuspiciousReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Shipping & Fulfillment Tracking Fields (Amazon/Flipkart Automatic Workflow)
        [MaxLength(50)]
        public string CurrentTrackingStatus { get; set; } = "OrderPlaced";

        [MaxLength(100)]
        public string TrackingNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CarrierName { get; set; } = "DHL Express";

        [MaxLength(500)]
        public string TrackingUrl { get; set; } = string.Empty;

        public DateTime? EstimatedDeliveryDate { get; set; }

        public DateTime? ShipmentBookedAt { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public virtual ICollection<OrderTrackingHistory> TrackingHistory { get; set; } = new List<OrderTrackingHistory>();
    }
}
