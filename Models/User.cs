using System.ComponentModel.DataAnnotations;

namespace SAT1.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Customer"; // Customer, VIP, Admin

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
