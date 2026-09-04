using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("products")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long ProductId { get; set; }

        [NotMapped]
        public string Id => ProductId.ToString();

        [Required(ErrorMessage = "Product Title is required.")]
        [MaxLength(255)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [NotMapped]
        public string ProductName
        {
            get => Title;
            set => Title = value;
        }

        [Required(ErrorMessage = "Product Slug is required.")]
        [MaxLength(255)]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [NotMapped]
        public string ProductSlug
        {
            get => Slug;
            set => Slug = value;
        }

        [Required(ErrorMessage = "Price is required.")]
        [Column("price", TypeName = "numeric(18,2)")]
        public decimal Price { get; set; }

        [NotMapped]
        public decimal BasePriceUSD
        {
            get => Price;
            set => Price = value;
        }

        [Required(ErrorMessage = "Category ID is required.")]
        [Column("category_id")]
        public long CategoryId { get; set; }

        [Required(ErrorMessage = "Diamond Shape ID is required.")]
        [Column("diamond_shape_id")]
        public long DiamondShapeId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Legacy / Presentation Helper Properties
        [NotMapped]
        public string SKU { get; set; } = string.Empty;

        [NotMapped]
        public string DefaultMetalType { get; set; } = "14K Yellow Gold";

        [NotMapped]
        public string DefaultPurity { get; set; } = "14K";

        [NotMapped]
        public decimal DefaultCaratWeight { get; set; } = 1.50m;

        [NotMapped]
        public string? DiamondClarity { get; set; } = "VVS1";

        [NotMapped]
        public string? DiamondColor { get; set; } = "E";

        [NotMapped]
        public string Description { get; set; } = "Bespoke fine jewelry piece hand-crafted with conflict-free diamonds and precision 3D CAD accuracy.";

        [NotMapped]
        public decimal? GrossWeightGram { get; set; } = 4.2m;

        [NotMapped]
        public bool IsAvailable { get; set; } = true;

        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        [ForeignKey("DiamondShapeId")]
        public virtual DiamondShape? DiamondShape { get; set; }

        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}
