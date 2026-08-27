using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("dynamic_pricing_rules")]
    public class DynamicPricingRule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("rule_type")]
        public string RuleType { get; set; } = "Metal"; // "Metal" or "Carat"

        [Required]
        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty; // e.g. "10k_gold", "14k_gold", "18k_gold", "platinum_950", "1.00_ct", "1.50_ct"

        [Required]
        [MaxLength(100)]
        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty; // e.g. "14K Gold", "18K Gold", "1.50 CT"

        [Required]
        [Column("price_offset_usd", TypeName = "numeric(18,2)")]
        public decimal PriceOffsetUSD { get; set; } = 0.00m;

        [Column("display_order")]
        public int DisplayOrder { get; set; } = 1;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DynamicPricingRuleDto
    {
        public long Id { get; set; }
        public string RuleType { get; set; } = "Metal";
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public decimal PriceOffsetUSD { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
