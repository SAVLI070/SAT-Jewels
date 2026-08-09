using System.ComponentModel.DataAnnotations;

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
        public decimal PriceUSD { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public string GalleryImages { get; set; } = string.Empty; // Comma-separated multi-angle photo URLs

        public string MetalOptions { get; set; } = "18K Yellow Gold (+0)|18K White Gold (+0)|18K Rose Gold (+0)|22K Yellow Gold (+150)|24K Pure Gold (+400)|Platinum 950 (+350)|14K Yellow Gold (-100)|14K White Gold (-100)|10K Solid Gold (-200)|Rose Platinum (+500)";

        public string CaratOptions { get; set; } = "0.5ct GIA (-800)|0.75ct GIA (-500)|1.0ct GIA (-400)|1.25ct GIA (-200)|1.5ct GIA (+0)|1.75ct GIA (+400)|2.0ct GIA (+750)|2.5ct GIA (+1200)|3.0ct GIA (+2000)|5.0ct Solitaire (+5000)";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
