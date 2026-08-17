using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("Products")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ProductId { get; set; }

        [Required]
        public string CategoryId { get; set; } = "rings";

        [Required]
        [MaxLength(255)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ProductSlug { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SKU { get; set; } = string.Empty;

        [Column(TypeName = "numeric(18,2)")]
        public decimal BasePriceUSD { get; set; }

        [Required]
        [MaxLength(50)]
        public string DefaultMetalType { get; set; } = "14K Yellow Gold";

        [Required]
        [MaxLength(20)]
        public string DefaultPurity { get; set; } = "14K";

        [Column(TypeName = "numeric(6,2)")]
        public decimal DefaultCaratWeight { get; set; } = 1.50m;

        [MaxLength(20)]
        public string? DiamondClarity { get; set; } = "VVS1";

        [MaxLength(10)]
        public string? DiamondColor { get; set; } = "E";

        [Required]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "numeric(8,2)")]
        public decimal? GrossWeightGram { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}
