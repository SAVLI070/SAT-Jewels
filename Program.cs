using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Neon PostgreSQL Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SatJewelDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Controllers and Views
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/SignIn";
        options.AccessDeniedPath = "/Account/SignIn";
    });

var app = builder.Build();

// Ensure Neon PostgreSQL Database Tables & Seed Data
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SatJewelDbContext>();
        
        // Execute DDL Raw SQL to guarantee tables exist in Neon PostgreSQL
        var createTablesSql = @"
            CREATE TABLE IF NOT EXISTS ""Categories"" (
                ""Id"" text NOT NULL,
                ""Name"" text NOT NULL,
                ""Badge"" text NOT NULL DEFAULT 'Popular',
                ""Subtitle"" text NOT NULL DEFAULT '',
                ""ImageUrl"" text NOT NULL DEFAULT '',
                ""DisplayOrder"" integer NOT NULL DEFAULT 1,
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_Categories"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""CatalogItems"" (
                ""Id"" text NOT NULL,
                ""Name"" text NOT NULL,
                ""CategoryId"" text NOT NULL DEFAULT 'rings',
                ""Spec"" text NOT NULL DEFAULT '',
                ""PriceUSD"" numeric NOT NULL DEFAULT 0.0,
                ""ImageUrl"" text NOT NULL DEFAULT '',
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_CatalogItems"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" text NOT NULL,
                ""FullName"" text NOT NULL,
                ""Email"" text NOT NULL,
                ""Phone"" text NOT NULL DEFAULT '',
                ""Password"" text NOT NULL,
                ""Role"" text NOT NULL DEFAULT 'Customer',
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""Orders"" (
                ""OrderId"" text NOT NULL,
                ""ItemName"" text NOT NULL,
                ""Amount"" numeric NOT NULL DEFAULT 0.0,
                ""Currency"" text NOT NULL DEFAULT 'USD',
                ""CustomerRegion"" text NOT NULL DEFAULT 'Global',
                ""PaymentMethod"" text NOT NULL DEFAULT 'Stripe USD',
                ""Status"" text NOT NULL DEFAULT 'Processing',
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_Orders"" PRIMARY KEY (""OrderId"")
            );
        ";

        db.Database.ExecuteSqlRaw(createTablesSql);

        // Seed initial categories if empty
        if (!db.Categories.Any())
        {
            db.Categories.AddRange(
                new Category { Id = "rings", Name = "Rings Collection", Badge = "Top Selling", Subtitle = "Solitaires & Halos", ImageUrl = "assets/ring_1.jpg", DisplayOrder = 1 },
                new Category { Id = "necklaces", Name = "Necklaces Section", Badge = "Popular", Subtitle = "Chokers & Pendants", ImageUrl = "assets/necklace_1.jpg", DisplayOrder = 2 },
                new Category { Id = "earrings", Name = "Earrings Section", Badge = "Trending", Subtitle = "Studs & Drops", ImageUrl = "assets/earring_card.jpg", DisplayOrder = 3 },
                new Category { Id = "bracelets", Name = "Bracelets Section", Badge = "Featured", Subtitle = "Tennis & Bangles", ImageUrl = "assets/bracelet_card.jpg", DisplayOrder = 4 }
            );
            db.SaveChanges();
        }

        // Seed initial items if empty
        if (!db.CatalogItems.Any())
        {
            db.CatalogItems.AddRange(
                new CatalogItem { Id = "ring_1", Name = "Royal Solitaire Diamond Ring", CategoryId = "rings", Spec = "18K Gold | 1.5ct GIA VVS1, E Color | Brilliant Cut", PriceUSD = 2200, ImageUrl = "assets/ring_1.jpg" },
                new CatalogItem { Id = "ring_2", Name = "Halo Cushion Cut Engagement Ring", CategoryId = "rings", Spec = "Platinum 950 | 2.0ct Halo Setting | IF Clarity", PriceUSD = 2900, ImageUrl = "https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=800&q=80" },
                new CatalogItem { Id = "neck_1", Name = "Imperial Diamond Floral Pendant", CategoryId = "necklaces", Spec = "18K Yellow Gold | Marquise & Pear Cut Diamonds", PriceUSD = 4200, ImageUrl = "assets/necklace_1.jpg" },
                new CatalogItem { Id = "ear_1", Name = "Chandelier Diamond Drop Earrings", CategoryId = "earrings", Spec = "18K Gold | 2.2ct Triple Drop Diamonds", PriceUSD = 2500, ImageUrl = "assets/earring_card.jpg" },
                new CatalogItem { Id = "brac_1", Name = "Classic Diamond Tennis Bracelet", CategoryId = "bracelets", Spec = "Platinum 950 | 5.0ct Total Weight | Round Cut", PriceUSD = 4700, ImageUrl = "assets/bracelet_card.jpg" }
            );
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Neon PostgreSQL database init error: {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
