using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("ProductImages")]
    public class ProductImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ImageId { get; set; }

        [Required(ErrorMessage = "Product ID reference is required.")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "Image URL is required.")]
        [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters.")]
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsMainImage { get; set; } = false;

        public int DisplayOrder { get; set; } = 0;

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}
