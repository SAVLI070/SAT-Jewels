using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public class Order
    {
        [Key]
        public string OrderId { get; set; } = Guid.NewGuid().ToString();

        public string ItemName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "USD";

        public string CustomerRegion { get; set; } = "United States";

        public string PaymentMethod { get; set; } = "Credit Card (Stripe USD)";

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string UserId { get; set; } = string.Empty;

        public string CustomerEmail { get; set; } = string.Empty;

        public string StripeSessionId { get; set; } = string.Empty;

        public string StripePaymentIntentId { get; set; } = string.Empty;

        public string ItemsJson { get; set; } = "[]";
    }
}
