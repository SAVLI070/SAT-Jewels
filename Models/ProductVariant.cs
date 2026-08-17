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

        [Required]
        public long ProductId { get; set; }

        [Required]
        [MaxLength(20)]
        public string RingSize { get; set; } = "7.0";

        [Required]
        [MaxLength(50)]
        public string MetalType { get; set; } = "18K Yellow Gold";

        [Column(TypeName = "numeric(6,2)")]
        public decimal CaratWeight { get; set; } = 1.50m;

        [Column(TypeName = "numeric(18,2)")]
        public decimal PriceAdjustmentUSD { get; set; } = 0.00m;

        public int StockQuantity { get; set; } = 10;

        public bool IsAvailable { get; set; } = true;

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
