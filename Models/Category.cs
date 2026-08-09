using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public class Category
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString().ToLower();

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Badge { get; set; } = "Popular"; // Top Selling, Popular, Trending, Featured, New Arrival

        public string Subtitle { get; set; } = string.Empty; // e.g. Solitaires & Halos

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
