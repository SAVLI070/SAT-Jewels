using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("product_reviews")]
    public class ProductReview
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long ReviewId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("product_id")]
        public string ProductId { get; set; } = string.Empty;

        [MaxLength(250)]
        [Column("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("user_id")]
        public string? UserId { get; set; }

        [Required(ErrorMessage = "Your Name is required.")]
        [MaxLength(100)]
        [Column("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress]
        [MaxLength(255)]
        [Column("customer_email")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        [Column("rating")]
        public int Rating { get; set; } = 5;

        [Required(ErrorMessage = "Review Title is required.")]
        [MaxLength(200)]
        [Column("review_title")]
        public string ReviewTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Review feedback is required.")]
        [MaxLength(2000)]
        [Column("review_text")]
        public string ReviewText { get; set; } = string.Empty;

        [Column("is_verified_buyer")]
        public bool IsVerifiedBuyer { get; set; } = true;

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Approved"; // "Approved", "Pending", "Rejected"

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
