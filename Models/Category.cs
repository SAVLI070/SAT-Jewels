using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("categories")]
    public class Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long CategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [MaxLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required.")]
        [MaxLength(150)]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [NotMapped]
        public string Id
        {
            get => CategoryId.ToString();
            set { if (long.TryParse(value, out long parseId)) CategoryId = parseId; }
        }

        [NotMapped]
        public long? ParentCategoryId { get; set; }

        [NotMapped]
        public string CategoryType { get; set; } = "Main Category";

        [NotMapped]
        public string SubCategoryName { get; set; } = string.Empty;

        [NotMapped]
        public string DiamondType { get; set; } = "Lab Grown Diamond";

        [NotMapped]
        public string DiamondCutShape { get; set; } = "All Shapes";

        [NotMapped]
        public string Badge { get; set; } = "Popular";

        [NotMapped]
        public string Subtitle { get; set; } = "Bespoke Fine Jewelry & AI Diamond Valuation";

        [NotMapped]
        public string ImageUrl { get; set; } = "/assets/categories/cat_engagement_rings.png";

        [NotMapped]
        public int DisplayOrder { get; set; } = 1;

        [NotMapped]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public virtual Category? ParentCategory { get; set; }

        [NotMapped]
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    }
}
