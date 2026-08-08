using Microsoft.EntityFrameworkCore;

namespace SAT1.Models
{
    public class SatJewelDbContext : DbContext
    {
        public SatJewelDbContext(DbContextOptions<SatJewelDbContext> options) : base(options) { }

        public DbSet<CatalogItem> CatalogItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed initial store items if database is freshly created
            modelBuilder.Entity<CatalogItem>().HasData(
                new CatalogItem
                {
                    Id = "ring_1",
                    Name = "Royal Solitaire Diamond Ring",
                    Category = "rings",
                    Spec = "18K Gold | 1.5ct GIA VVS1, E Color | Brilliant Cut",
                    PriceINR = 185000,
                    ImageUrl = "assets/ring_1.jpg"
                },
                new CatalogItem
                {
                    Id = "neck_1",
                    Name = "Imperial Diamond Floral Pendant",
                    Category = "necklaces",
                    Spec = "18K Yellow Gold | Marquise & Pear Cut Diamonds",
                    PriceINR = 350000,
                    ImageUrl = "assets/necklace_1.jpg"
                },
                new CatalogItem
                {
                    Id = "ear_1",
                    Name = "Chandelier Diamond Drop Earrings",
                    Category = "earrings",
                    Spec = "18K Gold | 2.2ct Triple Drop Diamonds",
                    PriceINR = 210000,
                    ImageUrl = "assets/earring_card.jpg"
                },
                new CatalogItem
                {
                    Id = "brac_1",
                    Name = "Classic Diamond Tennis Bracelet",
                    Category = "bracelets",
                    Spec = "Platinum 950 | 5.0ct Total Weight | Round Cut",
                    PriceINR = 390000,
                    ImageUrl = "assets/bracelet_card.jpg"
                }
            );
        }
    }
}
