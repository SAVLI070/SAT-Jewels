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

// Register BAL (Business Access Layer) Helpers matching FarmBridge Architecture
builder.Services.AddScoped<SAT1.BAL.CatalogBal>();
builder.Services.AddScoped<SAT1.BAL.AdminBal>();
builder.Services.AddScoped<SAT1.BAL.AuthBal>();

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
        
        // Execute DDL Raw SQL to guarantee tables and columns exist in Neon PostgreSQL
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
                ""GalleryImages"" text NOT NULL DEFAULT '',
                ""MetalOptions"" text NOT NULL DEFAULT '18K Yellow Gold (+0)|18K White Gold (+0)|18K Rose Gold (+0)|22K Yellow Gold (+150)|24K Pure Gold (+400)|Platinum 950 (+350)|14K Yellow Gold (-100)|14K White Gold (-100)|10K Solid Gold (-200)|Rose Platinum (+500)',
                ""CaratOptions"" text NOT NULL DEFAULT '0.5ct GIA (-800)|0.75ct GIA (-500)|1.0ct GIA (-400)|1.25ct GIA (-200)|1.5ct GIA (+0)|1.75ct GIA (+400)|2.0ct GIA (+750)|2.5ct GIA (+1200)|3.0ct GIA (+2000)|5.0ct Solitaire (+5000)',
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_CatalogItems"" PRIMARY KEY (""Id"")
            );

            ALTER TABLE ""CatalogItems"" ADD COLUMN IF NOT EXISTS ""GalleryImages"" text NOT NULL DEFAULT '';
            ALTER TABLE ""CatalogItems"" ADD COLUMN IF NOT EXISTS ""MetalOptions"" text NOT NULL DEFAULT '18K Yellow Gold (+0)|18K White Gold (+0)|18K Rose Gold (+0)|22K Yellow Gold (+150)|24K Pure Gold (+400)|Platinum 950 (+350)|14K Yellow Gold (-100)|14K White Gold (-100)|10K Solid Gold (-200)|Rose Platinum (+500)';
            ALTER TABLE ""CatalogItems"" ADD COLUMN IF NOT EXISTS ""CaratOptions"" text NOT NULL DEFAULT '0.5ct GIA (-800)|0.75ct GIA (-500)|1.0ct GIA (-400)|1.25ct GIA (-200)|1.5ct GIA (+0)|1.75ct GIA (+400)|2.0ct GIA (+750)|2.5ct GIA (+1200)|3.0ct GIA (+2000)|5.0ct Solitaire (+5000)';

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

        // Update existing Neon PostgreSQL catalog items with multi-angle gallery image paths
        var updateGallerySql = @"
            UPDATE ""CatalogItems"" SET ""ImageUrl"" = 'assets/ring_main.png', ""GalleryImages"" = 'assets/ring_main.png,assets/ring_angle.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'ring_1';
            UPDATE ""CatalogItems"" SET ""ImageUrl"" = 'assets/ring_angle.png', ""GalleryImages"" = 'assets/ring_angle.png,assets/ring_whitegold.png,assets/ring_rosegold.png,assets/ring_model.png' WHERE ""Id"" = 'ring_2';
            UPDATE ""CatalogItems"" SET ""ImageUrl"" = 'assets/necklace_main.png', ""GalleryImages"" = 'assets/necklace_main.png,assets/necklace_detail.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'neck_1';
            UPDATE ""CatalogItems"" SET ""ImageUrl"" = 'assets/earring_main.png', ""GalleryImages"" = 'assets/earring_main.png,assets/necklace_detail.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'ear_1';
            UPDATE ""CatalogItems"" SET ""ImageUrl"" = 'assets/bracelet_main.png', ""GalleryImages"" = 'assets/bracelet_main.png,assets/ring_whitegold.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'brac_1';
        ";

        try {
            db.Database.ExecuteSqlRaw(updateGallerySql);
        } catch { }

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
                new CatalogItem { Id = "ring_1", Name = "Royal Solitaire Diamond Ring", CategoryId = "rings", Spec = "18K Gold | 1.5ct GIA VVS1, E Color | Brilliant Cut", PriceUSD = 2200, ImageUrl = "assets/ring_main.png", GalleryImages = "assets/ring_main.png,assets/ring_angle.png,assets/ring_clarity.png,assets/ring_model.png" },
                new CatalogItem { Id = "ring_2", Name = "Halo Cushion Cut Engagement Ring", CategoryId = "rings", Spec = "Platinum 950 | 2.0ct Halo Setting | IF Clarity", PriceUSD = 2900, ImageUrl = "assets/ring_angle.png", GalleryImages = "assets/ring_main.png,assets/ring_angle.png,assets/ring_clarity.png,assets/ring_model.png" },
                new CatalogItem { Id = "neck_1", Name = "Imperial Diamond Floral Pendant", CategoryId = "necklaces", Spec = "18K Yellow Gold | Marquise & Pear Cut Diamonds", PriceUSD = 4200, ImageUrl = "assets/necklace_1.jpg", GalleryImages = "assets/necklace_1.jpg,assets/ring_clarity.png,assets/ring_model.png" },
                new CatalogItem { Id = "ear_1", Name = "Chandelier Diamond Drop Earrings", CategoryId = "earrings", Spec = "18K Gold | 2.2ct Triple Drop Diamonds", PriceUSD = 2500, ImageUrl = "assets/earring_card.jpg", GalleryImages = "assets/earring_card.jpg,assets/ring_clarity.png,assets/ring_model.png" },
                new CatalogItem { Id = "brac_1", Name = "Classic Diamond Tennis Bracelet", CategoryId = "bracelets", Spec = "Platinum 950 | 5.0ct Total Weight | Round Cut", PriceUSD = 4700, ImageUrl = "assets/bracelet_card.jpg", GalleryImages = "assets/bracelet_card.jpg,assets/ring_clarity.png,assets/ring_model.png" }
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
