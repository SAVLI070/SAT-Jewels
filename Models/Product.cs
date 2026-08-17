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

        [Required(ErrorMessage = "Category ID is required.")]
        [MaxLength(150, ErrorMessage = "Category ID cannot exceed 150 characters.")]
        public string CategoryId { get; set; } = "rings";

        [Required(ErrorMessage = "Product Name is required.")]
        [MaxLength(255, ErrorMessage = "Product Name cannot exceed 255 characters.")]
        [MinLength(2, ErrorMessage = "Product Name must be at least 2 characters.")]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product Slug is required.")]
        [MaxLength(255, ErrorMessage = "Product Slug cannot exceed 255 characters.")]
        public string ProductSlug { get; set; } = string.Empty;

        [Required(ErrorMessage = "SKU code is required.")]
        [MaxLength(100, ErrorMessage = "SKU code cannot exceed 100 characters.")]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "Base Price in USD is required.")]
        [Range(0.01, 1000000.00, ErrorMessage = "Base Price must be a positive amount.")]
        [Column(TypeName = "numeric(18,2)")]
        public decimal BasePriceUSD { get; set; }

        [Required(ErrorMessage = "Default Metal Type is required.")]
        [MaxLength(50, ErrorMessage = "Default Metal Type cannot exceed 50 characters.")]
        public string DefaultMetalType { get; set; } = "14K Yellow Gold";

        [Required(ErrorMessage = "Default Purity is required.")]
        [MaxLength(20, ErrorMessage = "Default Purity cannot exceed 20 characters.")]
        public string DefaultPurity { get; set; } = "14K";

        [Required(ErrorMessage = "Default Carat Weight is required.")]
        [Range(0.05, 50.00, ErrorMessage = "Carat weight must be between 0.05ct and 50.00ct.")]
        [Column(TypeName = "numeric(6,2)")]
        public decimal DefaultCaratWeight { get; set; } = 1.50m;

        [MaxLength(20)]
        public string? DiamondClarity { get; set; } = "VVS1";

        [MaxLength(10)]
        public string? DiamondColor { get; set; } = "E";

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(4000, ErrorMessage = "Description cannot exceed 4000 characters.")]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "numeric(8,2)")]
        public decimal? GrossWeightGram { get; set; }

        public bool IsAvailable { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}
