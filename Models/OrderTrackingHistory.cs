using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("order_tracking_history")]
    public class OrderTrackingHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long TrackingId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "OrderPlaced"; 
        // Statuses: OrderPlaced, Processing, ShipmentBooked, Shipped, InTransit, CustomsClearance, OutForDelivery, Delivered, Exception, Cancelled, Returned, Refunded

        [MaxLength(500)]
        [Column("status_note")]
        public string StatusNote { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("carrier_name")]
        public string CarrierName { get; set; } = "DHL Express";

        [MaxLength(100)]
        [Column("tracking_number")]
        public string TrackingNumber { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("tracking_url")]
        public string TrackingUrl { get; set; } = string.Empty;

        [MaxLength(200)]
        [Column("location")]
        public string Location { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("source")]
        public string Source { get; set; } = "System"; // "System" (automated webhook/API) or "Manual"

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
    }
}
