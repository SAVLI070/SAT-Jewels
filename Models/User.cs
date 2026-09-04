using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAT1.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [Column("Id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [Column("FullName")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Column("Phone")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [Column("Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Column("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("Role")]
        public string Role { get; set; } = "Customer"; // Customer, VIP, Admin

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
