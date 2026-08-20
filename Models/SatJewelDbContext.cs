using Microsoft.EntityFrameworkCore;

namespace SAT1.Models
{
    public class SatJewelDbContext : DbContext
    {
        public SatJewelDbContext(DbContextOptions<SatJewelDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<CatalogItem> CatalogItems { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductVariant> ProductVariants { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<UserAddress> UserAddresses { get; set; } = null!;
        public DbSet<MetalOption> MetalOptions { get; set; } = null!;
        public DbSet<CaratOption> CaratOptions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Keep Admin & Sample VIP Customer accounts
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = "user_admin",
                    FullName = "SAT Administrator",
                    Email = "admin@satjewels.com",
                    Phone = "+18005557285",
                    Password = "admin",
                    Role = "Admin"
                },
                new User
                {
                    Id = "user_vip_client",
                    FullName = "Eleanor Vance",
                    Email = "client@satjewels.com",
                    Phone = "+12125550199",
                    Password = "password123",
                    Role = "VIP"
                }
            );
        }
    }
}
