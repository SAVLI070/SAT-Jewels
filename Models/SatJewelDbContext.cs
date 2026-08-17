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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed Admin & Sample VIP Customer
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

            // Seed Jewelry Categories shown on Main Landing Page
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    Id = "rings",
                    Name = "Rings Collection",
                    Badge = "Top Selling",
                    Subtitle = "Solitaires & Halos",
                    ImageUrl = "assets/ring_1.jpg",
                    DisplayOrder = 1
                },
                new Category
                {
                    Id = "necklaces",
                    Name = "Necklaces Section",
                    Badge = "Popular",
                    Subtitle = "Chokers & Pendants",
                    ImageUrl = "assets/necklace_1.jpg",
                    DisplayOrder = 2
                },
                new Category
                {
                    Id = "earrings",
                    Name = "Earrings Section",
                    Badge = "Trending",
                    Subtitle = "Studs & Drops",
                    ImageUrl = "assets/earring_card.jpg",
                    DisplayOrder = 3
                },
                new Category
                {
                    Id = "bracelets",
                    Name = "Bracelets Section",
                    Badge = "Featured",
                    Subtitle = "Tennis & Bangles",
                    ImageUrl = "assets/bracelet_card.jpg",
                    DisplayOrder = 4
                }
            );

            // Seed initial store items in USD
            modelBuilder.Entity<CatalogItem>().HasData(
                new CatalogItem
                {
                    Id = "ring_1",
                    Name = "Royal Solitaire Diamond Ring",
                    CategoryId = "rings",
                    Spec = "18K Gold | 1.5ct GIA VVS1, E Color | Brilliant Cut",
                    PriceUSD = 2200,
                    ImageUrl = "assets/ring_main.png",
                    GalleryImages = "assets/ring_main.png,assets/ring_angle.png,assets/ring_clarity.png,assets/ring_model.png"
                },
                new CatalogItem
                {
                    Id = "ring_2",
                    Name = "Halo Cushion Cut Engagement Ring",
                    CategoryId = "rings",
                    Spec = "Platinum 950 | 2.0ct Halo Setting | IF Clarity",
                    PriceUSD = 2900,
                    ImageUrl = "assets/ring_angle.png",
                    GalleryImages = "assets/ring_angle.png,assets/ring_whitegold.png,assets/ring_rosegold.png,assets/ring_model.png"
                },
                new CatalogItem
                {
                    Id = "neck_1",
                    Name = "Imperial Diamond Floral Pendant",
                    CategoryId = "necklaces",
                    Spec = "18K Yellow Gold | Marquise & Pear Cut Diamonds",
                    PriceUSD = 4200,
                    ImageUrl = "assets/necklace_main.png",
                    GalleryImages = "assets/necklace_main.png,assets/necklace_detail.png,assets/ring_clarity.png,assets/ring_model.png"
                },
                new CatalogItem
                {
                    Id = "ear_1",
                    Name = "Chandelier Diamond Drop Earrings",
                    CategoryId = "earrings",
                    Spec = "18K Gold | 2.2ct Triple Drop Diamonds",
                    PriceUSD = 2500,
                    ImageUrl = "assets/earring_main.png",
                    GalleryImages = "assets/earring_main.png,assets/necklace_detail.png,assets/ring_clarity.png,assets/ring_model.png"
                },
                new CatalogItem
                {
                    Id = "brac_1",
                    Name = "Classic Diamond Tennis Bracelet",
                    CategoryId = "bracelets",
                    Spec = "Platinum 950 | 5.0ct Total Weight | Round Cut",
                    PriceUSD = 4700,
                    ImageUrl = "assets/bracelet_main.png",
                    GalleryImages = "assets/bracelet_main.png,assets/ring_whitegold.png,assets/ring_clarity.png,assets/ring_model.png"
                }
            );
        }
    }
}
