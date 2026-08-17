using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Neon PostgreSQL Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SatJewelDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    }));

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
        options.Cookie.Name = "SATJewel_AuthSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
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

            ALTER TABLE ""Categories"" ADD COLUMN IF NOT EXISTS ""ParentId"" text;
            ALTER TABLE ""Categories"" ADD COLUMN IF NOT EXISTS ""CategoryType"" text NOT NULL DEFAULT 'Main Category';
            ALTER TABLE ""Categories"" ADD COLUMN IF NOT EXISTS ""SubCategoryName"" text NOT NULL DEFAULT '';
            ALTER TABLE ""Categories"" ADD COLUMN IF NOT EXISTS ""DiamondType"" text NOT NULL DEFAULT 'Lab Grown Diamond';
            ALTER TABLE ""Categories"" ADD COLUMN IF NOT EXISTS ""DiamondCutShape"" text NOT NULL DEFAULT 'All Shapes';

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

        // Update existing Neon PostgreSQL catalog items with multi-angle gallery image paths & unguessable IDs
        var updateGallerySql = @"
            UPDATE ""CatalogItems"" SET ""Id"" = 'sat-prod-8f3a9b2c1d4e', ""ImageUrl"" = 'assets/ring_main.png', ""GalleryImages"" = 'assets/ring_main.png,assets/ring_angle.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'ring_1';
            UPDATE ""CatalogItems"" SET ""Id"" = 'sat-prod-7c9e6679425a', ""ImageUrl"" = 'assets/ring_angle.png', ""GalleryImages"" = 'assets/ring_angle.png,assets/ring_whitegold.png,assets/ring_rosegold.png,assets/ring_model.png' WHERE ""Id"" = 'ring_2';
            UPDATE ""CatalogItems"" SET ""Id"" = 'sat-prod-4b2a9f1c8e3d', ""ImageUrl"" = 'assets/necklace_main.png', ""GalleryImages"" = 'assets/necklace_main.png,assets/necklace_detail.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'neck_1';
            UPDATE ""CatalogItems"" SET ""Id"" = 'sat-prod-3f8d2b1a9c4e', ""ImageUrl"" = 'assets/earring_main.png', ""GalleryImages"" = 'assets/earring_main.png,assets/necklace_detail.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'ear_1';
            UPDATE ""CatalogItems"" SET ""Id"" = 'sat-prod-9e4a2c1b8f3d', ""ImageUrl"" = 'assets/bracelet_main.png', ""GalleryImages"" = 'assets/bracelet_main.png,assets/ring_whitegold.png,assets/ring_clarity.png,assets/ring_model.png' WHERE ""Id"" = 'brac_1';
        ";

        try {
            db.Database.ExecuteSqlRaw(updateGallerySql);
        } catch { }

        // Auto-seeding disabled per user directive: "Do not Feed in this command anything in any data tables"
        // Database tables remain 100% clean for manual user data feeding
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

app.UseStatusCodePagesWithReExecute("/Home/Restricted");

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0, private";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "-1";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self' https: data: 'unsafe-inline' 'unsafe-eval';";
        return Task.CompletedTask;
    });
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
