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

        [Required(ErrorMessage = "Order ID reference is required.")]
        public string OrderId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product ID reference is required.")]
        public long ProductId { get; set; }

        public long? VariantId { get; set; }

        [MaxLength(100, ErrorMessage = "Custom engraving text cannot exceed 100 characters.")]
        public string? CustomEngravingText { get; set; }

        [Range(1, 100, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Unit Price in USD is required.")]
        [Column(TypeName = "numeric(18,2)")]
        public decimal UnitPriceUSD { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant? Variant { get; set; }
    }
}
