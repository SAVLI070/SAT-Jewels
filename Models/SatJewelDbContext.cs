using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SAT1.Models
{
    public class SatJewelDbContext : DbContext
    {
        public SatJewelDbContext(DbContextOptions<SatJewelDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<DiamondShape> DiamondShapes { get; set; } = null!;
        public DbSet<Metal> Metals { get; set; } = null!;
        public DbSet<CaratOption> CaratOptions { get; set; } = null!;
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
        public DbSet<WishlistItem> WishlistItems { get; set; } = null!;
        public DbSet<DynamicPricingRule> DynamicPricingRules { get; set; } = null!;
        public DbSet<OrderTrackingHistory> OrderTrackingHistory { get; set; } = null!;
        public DbSet<ProductReview> ProductReviews { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<DateTimeToUtcConverter>();
            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<NullableDateTimeToUtcConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Indexes & Relationships for clean performance
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.CategoryId);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.DiamondShapeId);

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.CategoryId, p.DiamondShapeId });

            modelBuilder.Entity<ProductImage>()
                .HasIndex(pi => pi.ProductId);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(pv => pv.ProductId);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(pv => pv.MetalId);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(pv => pv.CaratId);

            modelBuilder.Entity<ProductVariant>()
                .HasIndex(pv => new { pv.ProductId, pv.MetalId, pv.CaratId });

            // Seed Admin Users
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = "user_admin_01",
                    FullName = "SAT Administrator",
                    Email = "admin@satjewel.com",
                    Phone = "+91 76987 27798",
                    Password = "AEt7jWRBQ8tWWyQ9pfdeqth4t26Lwq8NID6cCWMxJFo=",
                    PasswordHash = "AEt7jWRBQ8tWWyQ9pfdeqth4t26Lwq8NID6cCWMxJFo=",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new User
                {
                    Id = "user_admin_02",
                    FullName = "Dharmik (Founder & Lead Designer)",
                    Email = "admin@satjewels.com",
                    Phone = "+91 76987 27798",
                    Password = "AEt7jWRBQ8tWWyQ9pfdeqth4t26Lwq8NID6cCWMxJFo=",
                    PasswordHash = "AEt7jWRBQ8tWWyQ9pfdeqth4t26Lwq8NID6cCWMxJFo=",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new User
                {
                    Id = "user_admin_03",
                    FullName = "SAT Support Admin",
                    Email = "satjewels31@gmail.com",
                    Phone = "+91 76987 27798",
                    Password = "AEt7jWRBQ8tWWyQ9pfdeqth4t26Lwq8NID6cCWMxJFo=",
                    PasswordHash = "AEt7jWRBQ8tWWyQ9pfdeqth4t26Lwq8NID6cCWMxJFo=",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            );
        }
    }

    public class DateTimeToUtcConverter : ValueConverter<DateTime, DateTime>
    {
        public DateTimeToUtcConverter() : base(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => v)
        {
        }
    }

    public class NullableDateTimeToUtcConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableDateTimeToUtcConverter() : base(
            v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
            v => v)
        {
        }
    }
}
