using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("metals")]
    public class Metal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required(ErrorMessage = "Metal Name is required.")]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required.")]
        [MaxLength(150)]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("color_group")]
        public string ColorGroup { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("color_hex")]
        public string ColorHex { get; set; } = string.Empty;
    }
}
