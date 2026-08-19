using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("CaratOptions")]
    public class CaratOption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string CatalogItemId { get; set; } = string.Empty;

        [ForeignKey("CatalogItemId")]
        public virtual CatalogItem? CatalogItem { get; set; }

        [Required]
        public string CaratLabel { get; set; } = string.Empty;

        [Column(TypeName = "numeric(18,2)")]
        public decimal PriceOffsetUSD { get; set; } = 0.00m;

        public int DisplayOrder { get; set; } = 1;
    }
}
