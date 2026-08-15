using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public class Category
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString().ToLower();

        [Required]
        public string Name { get; set; } = string.Empty;

        // Hierarchical Nested Category Fields
        public string? ParentId { get; set; } // Null for Main Parent Category, or ID of Parent Category

        public string CategoryType { get; set; } = "Main Category"; // Main Category, Sub Category, Diamond Cut / Stone Type

        public string SubCategoryName { get; set; } = string.Empty; // e.g. Engagement Ring, Wedding Band, Eternity Ring

        public string DiamondType { get; set; } = "Lab Grown Diamond"; // Lab Grown Diamond, Moissanite, Natural Diamond

        public string DiamondCutShape { get; set; } = "All Shapes"; // Rose Cut, Radiant Cut, Asscher Cut, Oval Cut, Emerald Cut, Round Cut, Marquise Cut, Princess Cut, Cushion Cut, Pear Cut, Kite Cut, Triangle Cut

        public string Badge { get; set; } = "Popular"; // Top Selling, Popular, Trending, Featured, New Arrival

        public string Subtitle { get; set; } = string.Empty; // e.g. Solitaires & Halos

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
