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

        private string? _imageUrl;

        [NotMapped]
        public string ImageUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(_imageUrl) && !_imageUrl.EndsWith("cat_engagement_rings.png"))
                    return _imageUrl;
                return GetDefaultImageUrl(CategoryId, Name, Slug);
            }
            set => _imageUrl = value;
        }

        public static string GetDefaultImageUrl(long categoryId, string? name, string? slug)
        {
            var clean = (name ?? slug ?? "").ToLower();
            if (categoryId == 1 || clean.Contains("engagement")) return "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366770/sat_jewels/categories/cat_1_engagement_rings.png";
            if (categoryId == 2 || clean.Contains("wedding ring") || (clean.Contains("wedding") && !clean.Contains("band"))) return "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366771/sat_jewels/categories/cat_2_wedding_rings.jpg";
            if (categoryId == 3 || clean.Contains("bridal") || clean.Contains("men")) return "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366773/sat_jewels/categories/cat_3_bridal_sets.jpg";
            if (categoryId == 4 || clean.Contains("earring")) return "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366775/sat_jewels/categories/cat_4_earrings.jpg";
            if (categoryId == 5 || clean.Contains("bracelet")) return "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366777/sat_jewels/categories/cat_5_bracelets.jpg";
            if (categoryId == 6 || clean.Contains("necklace") || clean.Contains("pendant")) return "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366779/sat_jewels/categories/cat_6_necklaces.jpg";

            return categoryId switch
            {
                1 => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366770/sat_jewels/categories/cat_1_engagement_rings.png",
                2 => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366771/sat_jewels/categories/cat_2_wedding_rings.jpg",
                3 => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366773/sat_jewels/categories/cat_3_bridal_sets.jpg",
                4 => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366775/sat_jewels/categories/cat_4_earrings.jpg",
                5 => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366777/sat_jewels/categories/cat_5_bracelets.jpg",
                6 => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366779/sat_jewels/categories/cat_6_necklaces.jpg",
                _ => "https://res.cloudinary.com/ihcs8m6o/image/upload/v1788366770/sat_jewels/categories/cat_1_engagement_rings.png"
            };
        }

        [NotMapped]
        public int DisplayOrder { get; set; } = 1;

        [NotMapped]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public virtual Category? ParentCategory { get; set; }

        [NotMapped]
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    }
}
