using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        [Required(ErrorMessage = "Category ID is required.")]
        [MaxLength(150, ErrorMessage = "Category ID / Slug cannot exceed 150 characters.")]
        public string Id { get; set; } = Guid.NewGuid().ToString().ToLower();

        [Required(ErrorMessage = "Category Name is required.")]
        [MaxLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        // Hierarchical Nested Category Fields (Self-referencing Foreign Key)
        [MaxLength(150)]
        public string? ParentId { get; set; } // Null for Main Parent Category, or ID of Parent Category

        [MaxLength(50)]
        public string CategoryType { get; set; } = "Main Category"; // Main Category, Sub Category, Diamond Cut / Stone Type

        [MaxLength(100)]
        public string SubCategoryName { get; set; } = string.Empty; // e.g. Engagement Ring, Wedding Band, Eternity Ring

        [MaxLength(50)]
        public string DiamondType { get; set; } = "Lab Grown Diamond"; // Lab Grown Diamond, Moissanite, Natural Diamond

        [MaxLength(50)]
        public string DiamondCutShape { get; set; } = "All Shapes"; // Rose Cut, Radiant Cut, Oval Cut, etc.

        [MaxLength(50)]
        public string Badge { get; set; } = "Popular"; // Top Selling, Popular, Trending, Featured, New Arrival

        [MaxLength(200)]
        public string Subtitle { get; set; } = string.Empty; // e.g. Solitaires & Halos

        [Required(ErrorMessage = "Image URL is required.")]
        [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters.")]
        public string ImageUrl { get; set; } = string.Empty;

        [Range(1, 1000, ErrorMessage = "Display Order must be between 1 and 1000.")]
        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ParentId")]
        public virtual Category? ParentCategory { get; set; }

        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    }
}
