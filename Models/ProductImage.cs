using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("product_images")]
    public class ProductImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long ImageId { get; set; }

        [NotMapped]
        public long Id => ImageId;

        [Required(ErrorMessage = "Product ID reference is required.")]
        [Column("product_id")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "Image Path is required.")]
        [MaxLength(500)]
        [Column("image_path")]
        public string ImagePath { get; set; } = string.Empty;

        [NotMapped]
        public string ImageUrl
        {
            get => ImagePath;
            set => ImagePath = value;
        }

        [Column("display_order")]
        public int DisplayOrder { get; set; } = 1;

        [NotMapped]
        public bool IsMainImage { get; set; } = false;

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
