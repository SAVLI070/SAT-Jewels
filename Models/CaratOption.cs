using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("carat_options")]
    public class CaratOption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("carat_weight", TypeName = "numeric(6,2)")]
        public decimal CaratWeight { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        // Legacy Helper Properties
        [NotMapped]
        public string CatalogItemId { get; set; } = string.Empty;

        [NotMapped]
        public string CaratLabel
        {
            get => Label;
            set => Label = value;
        }

        [NotMapped]
        public decimal PriceOffsetUSD { get; set; } = 0.00m;

        [NotMapped]
        public int DisplayOrder { get; set; } = 1;
    }
}
