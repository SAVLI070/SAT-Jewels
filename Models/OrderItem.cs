using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long OrderItemId { get; set; }

        [Required]
        public string OrderId { get; set; } = string.Empty;

        [Required]
        public long ProductId { get; set; }

        public long? VariantId { get; set; }

        [MaxLength(100)]
        public string? CustomEngravingText { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "numeric(18,2)")]
        public decimal UnitPriceUSD { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant? Variant { get; set; }
    }
}
