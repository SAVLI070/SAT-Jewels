using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("diamond_shapes")]
    public class DiamondShape
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required(ErrorMessage = "Diamond Shape Name is required.")]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug is required.")]
        [MaxLength(150)]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("icon_url")]
        public string IconUrl { get; set; } = string.Empty;
    }
}
