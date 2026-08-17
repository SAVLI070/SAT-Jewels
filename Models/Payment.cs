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

        [Required]
        public string OrderId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string PaymentGateway { get; set; } = "Stripe_International";

        [Required]
        [MaxLength(255)]
        public string GatewayTransactionId { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [Column(TypeName = "numeric(18,2)")]
        public decimal AmountUSD { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? SettlementINR { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Authorized";

        public byte[]? EncryptedGatewayPayload { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
    }
}
