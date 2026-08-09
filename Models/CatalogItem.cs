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

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
