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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long CategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [MaxLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required.")]
        [MaxLength(150)]
        public string Slug { get; set; } = string.Empty;

        // Hierarchical Self-referencing Foreign Key (long)
        public long? ParentCategoryId { get; set; }

        [MaxLength(50)]
        public string CategoryType { get; set; } = "Main Category";

        [MaxLength(100)]
        public string SubCategoryName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string DiamondType { get; set; } = "Lab Grown Diamond";

        [MaxLength(50)]
        public string DiamondCutShape { get; set; } = "All Shapes";

        [MaxLength(50)]
        public string Badge { get; set; } = "Popular";

        [MaxLength(200)]
        public string Subtitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Image URL is required.")]
        [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters.")]
        public string ImageUrl { get; set; } = string.Empty;

        [Range(1, 1000, ErrorMessage = "Display Order must be between 1 and 1000.")]
        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public string Id 
        { 
            get => CategoryId.ToString(); 
            set { if (long.TryParse(value, out long parseId)) CategoryId = parseId; } 
        }

        [ForeignKey("ParentCategoryId")]
        public virtual Category? ParentCategory { get; set; }

        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    }
}
