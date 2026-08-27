using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    public class CatalogItem
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string CategoryId { get; set; } = "rings"; // Foreign key to Category.Id (e.g., rings, necklaces)

        public string Spec { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        [NotMapped]
        public decimal PriceUSD 
        { 
            get => Price; 
            set => Price = value; 
        }

        public string ImageUrl { get; set; } = string.Empty;

        public string GalleryImages { get; set; } = string.Empty; // Comma-separated multi-angle photo URLs

        public string MetalOptions { get; set; } = string.Empty;

        public string CaratOptions { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public virtual ICollection<MetalOption> MetalOptionList { get; set; } = new List<MetalOption>();

        [NotMapped]
        public virtual ICollection<CaratOption> CaratOptionList { get; set; } = new List<CaratOption>();

        [NotMapped]
        public List<ProductVariantMatrixItemDto>? Variants { get; set; } = new List<ProductVariantMatrixItemDto>();
    }
}
