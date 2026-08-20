using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

var builder = WebApplication.CreateBuilder(args);

// Resolve & Validate PostgreSQL Connection String (Render / Neon Cloud DB)
var rawConn = builder.Configuration["DATABASE_URL"] 
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

string connectionString = "Host=ep-soft-sound-azkeypgg-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_yX8TV4rmHEqR;Ssl Mode=Require;Trust Server Certificate=true;";

if (!string.IsNullOrWhiteSpace(rawConn) && !rawConn.Contains("database-1.cluster-c0rk64yygjkf.us-east-1.rds.amazonaws.com"))
{
    if (rawConn.StartsWith("postgres://") || rawConn.StartsWith("postgresql://"))
    {
        try
        {
            var uri = new Uri(rawConn);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var dbName = uri.AbsolutePath.TrimStart('/');
            connectionString = $"Host={host};Port={port};Database={dbName};Username={user};Password={password};Ssl Mode=Require;Trust Server Certificate=true;";
        }
        catch
        {
            // Fallback to active Neon DB connection string if URL parsing fails
        }
    }
    else
    {
        connectionString = rawConn;
    }
}

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

// Add HttpClient for External Payment Gateways (PayPal & Razorpay)
builder.Services.AddHttpClient();

// Register BAL & DAL Payment Services
builder.Services.AddScoped<SAT1.DAL.OrderRepository>();
builder.Services.AddScoped<SAT1.BAL.CatalogBal>();
builder.Services.AddScoped<SAT1.BAL.AdminBal>();
builder.Services.AddScoped<SAT1.BAL.AuthBal>();
builder.Services.AddScoped<SAT1.BAL.PayPalService>();
builder.Services.AddScoped<SAT1.BAL.RazorpayService>();
builder.Services.AddScoped<SAT1.BAL.OrderBusinessService>();

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
            ALTER TABLE ""CatalogItems"" ADD COLUMN IF NOT EXISTS ""Price"" numeric NOT NULL DEFAULT 0.0;
            UPDATE ""CatalogItems"" SET ""Price"" = ""PriceUSD"" WHERE ""Price"" = 0 AND ""PriceUSD"" > 0;
            ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""Price"" numeric NOT NULL DEFAULT 0.0;
            UPDATE ""Products"" SET ""Price"" = ""BasePriceUSD"" WHERE ""Price"" = 0 AND ""BasePriceUSD"" > 0;

            CREATE TABLE IF NOT EXISTS ""MetalOptions"" (
                ""Id"" serial PRIMARY KEY,
                ""CatalogItemId"" text NOT NULL,
                ""MetalName"" text NOT NULL,
                ""PriceOffsetUSD"" numeric NOT NULL DEFAULT 0.0,
                ""DisplayOrder"" integer NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS ""CaratOptions"" (
                ""Id"" serial PRIMARY KEY,
                ""CatalogItemId"" text NOT NULL,
                ""CaratLabel"" text NOT NULL,
                ""PriceOffsetUSD"" numeric NOT NULL DEFAULT 0.0,
                ""DisplayOrder"" integer NOT NULL DEFAULT 1
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

            CREATE TABLE IF NOT EXISTS ""UserAddresses"" (
                ""AddressId"" text NOT NULL,
                ""UserId"" text NOT NULL,
                ""FullName"" text NOT NULL,
                ""Phone"" text NOT NULL DEFAULT '',
                ""StreetAddress"" text NOT NULL,
                ""ApartmentSuite"" text NOT NULL DEFAULT '',
                ""City"" text NOT NULL,
                ""State"" text NOT NULL,
                ""PostalCode"" text NOT NULL,
                ""Country"" text NOT NULL DEFAULT 'United States',
                ""IsDefault"" boolean NOT NULL DEFAULT false,
                CONSTRAINT ""PK_UserAddresses"" PRIMARY KEY (""AddressId"")
            );

            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PaymentProvider"" text NOT NULL DEFAULT 'PayPal';
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ProviderOrderId"" text NOT NULL DEFAULT '';
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentId"" text NOT NULL DEFAULT '';
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ExpectedAmount"" numeric NOT NULL DEFAULT 0.0;
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""AmountPaid"" numeric NOT NULL DEFAULT 0.0;
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PaidAt"" timestamp with time zone;
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""BuyerInfo"" text NOT NULL DEFAULT '';
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""IsSuspicious"" boolean NOT NULL DEFAULT false;
            ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""SuspiciousReason"" text;

            ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""ProviderOrderId"" text NOT NULL DEFAULT '';
            ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""SignatureVerified"" boolean NOT NULL DEFAULT false;
            ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""RawPayload"" text;
        ";

        db.Database.ExecuteSqlRaw(createTablesSql);

        // Seed & Reset 6 Core Categories (IDs 1..6) and Clear Product Data per user directive
        SAT1.BAL.RelationalDbSeeder.SeedRelationalData(db);
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
