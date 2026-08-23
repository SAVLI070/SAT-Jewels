using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("product_variants")]
    public class ProductVariant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long VariantId { get; set; }

        [NotMapped]
        public long Id => VariantId;

        [Required(ErrorMessage = "Product ID reference is required.")]
        [Column("product_id")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "Metal ID reference is required.")]
        [Column("metal_id")]
        public long MetalId { get; set; }

        [Column("carat_id")]
        public long? CaratId { get; set; }

        [Required(ErrorMessage = "SKU is required.")]
        [MaxLength(100)]
        [Column("sku")]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required.")]
        [Column("price", TypeName = "numeric(18,2)")]
        public decimal Price { get; set; }

        [Column("stock_quantity")]
        public int StockQuantity { get; set; } = 10;

        [MaxLength(500)]
        [Column("variant_image_path")]
        public string VariantImagePath { get; set; } = string.Empty;

        [Column("is_available")]
        public bool IsAvailable { get; set; } = true;

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [ForeignKey("MetalId")]
        public virtual Metal? Metal { get; set; }

        [ForeignKey("CaratId")]
        public virtual CaratOption? Carat { get; set; }

        // Legacy / Helper Properties
        [NotMapped]
        public string RingSize { get; set; } = "7.0";

        [NotMapped]
        public string MetalType => Metal != null ? Metal.Name : "14K Yellow Gold";

        [NotMapped]
        public decimal CaratWeight
        {
            get => Carat != null ? Carat.CaratWeight : 1.50m;
            set { }
        }

        [NotMapped]
        public decimal PriceAdjustmentUSD
        {
            get => Price - (Product != null ? Product.Price : 0m);
            set { }
        }
    }
}
