using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("Payments")]
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PaymentId { get; set; }

        [Required(ErrorMessage = "Order ID reference is required.")]
        public string OrderId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Payment Gateway is required.")]
        [MaxLength(50)]
        public string PaymentGateway { get; set; } = "Stripe_International";

        [Required(ErrorMessage = "Gateway Transaction ID is required.")]
        [MaxLength(255)]
        public string GatewayTransactionId { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [Required(ErrorMessage = "Amount in USD is required.")]
        [Column(TypeName = "numeric(18,2)")]
        public decimal AmountUSD { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SettlementINR { get; set; }

        [Required(ErrorMessage = "Payment Status is required.")]
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Authorized";

        [MaxLength(100)]
        public string ProviderOrderId { get; set; } = string.Empty;

        public bool SignatureVerified { get; set; } = false;

        public string? RawPayload { get; set; }

        public byte[]? EncryptedGatewayPayload { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
    }
}
