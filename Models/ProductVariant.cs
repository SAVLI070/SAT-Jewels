using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("ProductVariants")]
    public class ProductVariant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long VariantId { get; set; }

        [Required(ErrorMessage = "Product ID reference is required.")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "Ring Size is required.")]
        [MaxLength(20, ErrorMessage = "Ring Size cannot exceed 20 characters.")]
        public string RingSize { get; set; } = "7.0";

        [Required(ErrorMessage = "Metal Type is required.")]
        [MaxLength(50, ErrorMessage = "Metal Type cannot exceed 50 characters.")]
        public string MetalType { get; set; } = "18K Yellow Gold";

        [Required(ErrorMessage = "Carat Weight is required.")]
        [Column(TypeName = "numeric(6,2)")]
        public decimal CaratWeight { get; set; } = 1.50m;

        [Column(TypeName = "numeric(18,2)")]
        public decimal PriceAdjustmentUSD { get; set; } = 0.00m;

        [Range(0, 10000, ErrorMessage = "Stock Quantity cannot be negative.")]
        public int StockQuantity { get; set; } = 10;

        public bool IsAvailable { get; set; } = true;

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
