using System;
using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public class WishlistItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string CatalogItemId { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public decimal PriceUSD { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
