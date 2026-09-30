using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("custom_ring_inquiries")]
    public class CustomRingInquiry
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("full_name")]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("phone")]
        [MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [Column("category")]
        [MaxLength(100)]
        public string Category { get; set; } = "Engagement Ring";

        [Column("metal_preference")]
        [MaxLength(100)]
        public string? MetalPreference { get; set; }

        [Column("ring_size")]
        [MaxLength(50)]
        public string? RingSize { get; set; }

        [Column("target_budget")]
        [MaxLength(100)]
        public string? TargetBudget { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "New";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
