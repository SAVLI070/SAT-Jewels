using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public class Order
    {
        [Key]
        public string OrderId { get; set; } = Guid.NewGuid().ToString();

        public string ItemName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "INR";

        public string CustomerRegion { get; set; } = "India";

        public string PaymentMethod { get; set; } = "Credit Card";

        public string Status { get; set; } = "Pending GIA Dispatch";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
