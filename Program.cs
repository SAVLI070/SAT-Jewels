using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SAT1.Models;

// Prevent inotify instance exhaustion (limit 128) in Linux containers (Render, Docker, Kubernetes)
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Resolve & Validate PostgreSQL Connection String (Render / Neon Cloud DB)
var rawConn = builder.Configuration["DATABASE_URL"] 
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

string connectionString = "Host=satjewels-postgres.c4r4s48oeqi1.us-east-1.rds.amazonaws.com;Port=5432;Database=satjewels_db;Username=satjewels_admin;Password=SatJewels#Db2026!Secure;Ssl Mode=Require;Trust Server Certificate=true;";

if (!string.IsNullOrWhiteSpace(rawConn))
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
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }));

// Add Controllers and Views
builder.Services.AddControllersWithViews();

// Add HttpClient for External Payment Gateways (PayPal & Razorpay)
builder.Services.AddHttpClient();

// Add In-Memory Caching (for OTP security, rate limiting, and temporary token vaults)
builder.Services.AddMemoryCache();

// Register BAL & DAL Payment Services
builder.Services.AddScoped<SAT1.DAL.OrderRepository>();
builder.Services.AddScoped<SAT1.DAL.OrderTrackingRepository>();
builder.Services.AddScoped<SAT1.BAL.CatalogBal>();
builder.Services.AddScoped<SAT1.BAL.AdminBal>();
builder.Services.AddScoped<SAT1.BAL.AuthBal>();
builder.Services.AddScoped<SAT1.BAL.OtpService>();
builder.Services.AddScoped<SAT1.BAL.ReviewBal>();
builder.Services.AddScoped<SAT1.BAL.Shipping.UpsShippingProviderService>();
builder.Services.AddScoped<SAT1.BAL.Shipping.AramexShippingProviderService>();
builder.Services.AddScoped<SAT1.BAL.Shipping.UspsShippingProviderService>();
builder.Services.AddScoped<SAT1.BAL.Shipping.DefaultShippingProviderService>();
builder.Services.AddScoped<SAT1.BAL.Shipping.IShippingProviderService>(sp => sp.GetRequiredService<SAT1.BAL.Shipping.DefaultShippingProviderService>());
builder.Services.AddScoped<SAT1.BAL.EmailNotificationService>();
builder.Services.AddScoped<SAT1.BAL.OrderTrackingService>();
builder.Services.AddScoped<SAT1.BAL.PayPalService>();
builder.Services.AddScoped<SAT1.BAL.RazorpayService>();
builder.Services.AddScoped<SAT1.BAL.OrderBusinessService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/SignIn";
        options.AccessDeniedPath = "/Account/SignIn";
        options.Cookie.Name = "SATJewel_Session_v5";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Expiration = null;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    });

var app = builder.Build();

// Ensure Neon PostgreSQL Database Tables & Seed Data
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SatJewelDbContext>();
        
        // Execute DDL Statements individually to guarantee tables/columns exist in Neon PostgreSQL
        string[] ddlStatements = new[]
        {
            @"CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" text NOT NULL,
                ""FullName"" text NOT NULL,
                ""Email"" text NOT NULL,
                ""Phone"" text NOT NULL DEFAULT '',
                ""Password"" text NOT NULL,
                ""Role"" text NOT NULL DEFAULT 'Customer',
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id"")
            );",

            @"CREATE TABLE IF NOT EXISTS ""Orders"" (
                ""OrderId"" text NOT NULL,
                ""ItemName"" text NOT NULL,
                ""Amount"" numeric NOT NULL DEFAULT 0.0,
                ""Currency"" text NOT NULL DEFAULT 'USD',
                ""CustomerRegion"" text NOT NULL DEFAULT 'Global',
                ""PaymentMethod"" text NOT NULL DEFAULT 'Stripe USD',
                ""Status"" text NOT NULL DEFAULT 'Processing',
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_Orders"" PRIMARY KEY (""OrderId"")
            );",

            @"CREATE TABLE IF NOT EXISTS ""UserAddresses"" (
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
            );",

            @"CREATE TABLE IF NOT EXISTS ""dynamic_pricing_rules"" (
                ""id"" bigserial NOT NULL,
                ""rule_type"" text NOT NULL DEFAULT 'Metal',
                ""code"" text NOT NULL,
                ""display_name"" text NOT NULL,
                ""price_offset_usd"" numeric(18,2) NOT NULL DEFAULT 0.00,
                ""display_order"" integer NOT NULL DEFAULT 1,
                ""is_active"" boolean NOT NULL DEFAULT true,
                ""updated_at"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_dynamic_pricing_rules"" PRIMARY KEY (""id"")
            );",

            @"CREATE TABLE IF NOT EXISTS ""order_tracking_history"" (
                ""id"" bigserial NOT NULL,
                ""order_id"" text NOT NULL,
                ""status"" text NOT NULL DEFAULT 'OrderPlaced',
                ""status_note"" text NOT NULL DEFAULT '',
                ""carrier_name"" text NOT NULL DEFAULT 'DHL Express',
                ""tracking_number"" text NOT NULL DEFAULT '',
                ""tracking_url"" text NOT NULL DEFAULT '',
                ""location"" text NOT NULL DEFAULT '',
                ""source"" text NOT NULL DEFAULT 'System',
                ""created_at"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_order_tracking_history"" PRIMARY KEY (""id"")
            );",

            @"CREATE TABLE IF NOT EXISTS ""product_reviews"" (
                ""id"" bigserial NOT NULL,
                ""product_id"" text NOT NULL,
                ""product_name"" text NOT NULL DEFAULT '',
                ""user_id"" text,
                ""customer_name"" text NOT NULL,
                ""customer_email"" text NOT NULL,
                ""rating"" integer NOT NULL DEFAULT 5,
                ""review_title"" text NOT NULL,
                ""review_text"" text NOT NULL,
                ""is_verified_buyer"" boolean NOT NULL DEFAULT true,
                ""status"" text NOT NULL DEFAULT 'Approved',
                ""created_at"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_product_reviews"" PRIMARY KEY (""id"")
            );",

            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PaymentProvider"" text NOT NULL DEFAULT 'PayPal';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ProviderOrderId"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentId"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ExpectedAmount"" numeric NOT NULL DEFAULT 0.0;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""AmountPaid"" numeric NOT NULL DEFAULT 0.0;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PaidAt"" timestamp with time zone;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""BuyerInfo"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""IsSuspicious"" boolean NOT NULL DEFAULT false;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""SuspiciousReason"" text;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""CurrentTrackingStatus"" text NOT NULL DEFAULT 'OrderPlaced';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""TrackingNumber"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""CarrierName"" text NOT NULL DEFAULT 'DHL Express';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""TrackingUrl"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""EstimatedDeliveryDate"" timestamp with time zone;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipmentBookedAt"" timestamp with time zone;",

            @"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""ProviderOrderId"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""SignatureVerified"" boolean NOT NULL DEFAULT false;",
            @"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""RawPayload"" text;",

            // Ensure All 11 Fine Jewelry Metals (including 925 Sterling Silver)
            @"INSERT INTO metals (id, name, slug, color_group, color_hex)
            VALUES 
                (1, '10K Yellow Gold', '10k-yellow-gold', 'Yellow Gold', '#E5CA8F'),
                (2, '10K White Gold', '10k-white-gold', 'White Gold', '#D1D5DB'),
                (3, '10K Rose Gold', '10k-rose-gold', 'Rose Gold', '#E8A598'),
                (4, '14K Yellow Gold', '14k-yellow-gold', 'Yellow Gold', '#F2D06B'),
                (5, '14K White Gold', '14k-white-gold', 'White Gold', '#E5E7EB'),
                (6, '14K Rose Gold', '14k-rose-gold', 'Rose Gold', '#EAA396'),
                (7, '18K Yellow Gold', '18k-yellow-gold', 'Yellow Gold', '#FFD700'),
                (8, '18K White Gold', '18k-white-gold', 'White Gold', '#F5F5F5'),
                (9, '18K Rose Gold', '18k-rose-gold', 'Rose Gold', '#E68A7C'),
                (10, '950 Platinum', '950-platinum', 'Platinum', '#E5E4E2'),
                (11, '925 Sterling Silver', '925-sterling-silver', 'Silver', '#C0C0C0')
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, slug = EXCLUDED.slug, color_group = EXCLUDED.color_group, color_hex = EXCLUDED.color_hex;",

            // Ensure All 10 Diamond Shapes
            @"INSERT INTO diamond_shapes (id, name, slug, icon_url)
            VALUES
                (1, 'Round', 'round', '/assets/shapes/round.svg'),
                (2, 'Oval', 'oval', '/assets/shapes/oval.svg'),
                (3, 'Emerald', 'emerald', '/assets/shapes/emerald.svg'),
                (4, 'Marquise', 'marquise', '/assets/shapes/marquise.svg'),
                (5, 'Pear', 'pear', '/assets/shapes/pear.svg'),
                (6, 'Princess', 'princess', '/assets/shapes/princess.svg'),
                (7, 'Cushion', 'cushion', '/assets/shapes/cushion.svg'),
                (8, 'Radiant', 'radiant', '/assets/shapes/radiant.svg'),
                (9, 'Asscher', 'asscher', '/assets/shapes/asscher.svg'),
                (10, 'Heart', 'heart', '/assets/shapes/heart.svg')
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, slug = EXCLUDED.slug;",

            // Ensure All 9 Carat Options
            @"INSERT INTO carat_options (id, carat_weight, label, slug)
            VALUES
                (1, 0.50, '0.50 CT', '0.50-ct'),
                (2, 0.75, '0.75 CT', '0.75-ct'),
                (3, 1.00, '1.00 CT', '1.00-ct'),
                (4, 1.25, '1.25 CT', '1.25-ct'),
                (5, 1.50, '1.50 CT', '1.50-ct'),
                (6, 2.00, '2.00 CT', '2.00-ct'),
                (7, 3.00, '3.00 CT', '3.00-ct'),
                (8, 4.00, '4.00 CT', '4.00-ct'),
                (9, 5.00, '5.00 CT', '5.00-ct')
            ON CONFLICT (id) DO UPDATE SET carat_weight = EXCLUDED.carat_weight, label = EXCLUDED.label, slug = EXCLUDED.slug;",

            // Synchronize PostgreSQL Primary Key Sequences with MAX(id)
            @"DO $$
            BEGIN
                BEGIN PERFORM setval(pg_get_serial_sequence('products', 'id'), COALESCE((SELECT MAX(id) FROM products), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('product_variants', 'id'), COALESCE((SELECT MAX(id) FROM product_variants), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('product_images', 'id'), COALESCE((SELECT MAX(id) FROM product_images), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('categories', 'id'), COALESCE((SELECT MAX(id) FROM categories), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('diamond_shapes', 'id'), COALESCE((SELECT MAX(id) FROM diamond_shapes), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('metals', 'id'), COALESCE((SELECT MAX(id) FROM metals), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('carat_options', 'id'), COALESCE((SELECT MAX(id) FROM carat_options), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('dynamic_pricing_rules', 'id'), COALESCE((SELECT MAX(id) FROM dynamic_pricing_rules), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('product_reviews', 'id'), COALESCE((SELECT MAX(id) FROM product_reviews), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
                BEGIN PERFORM setval(pg_get_serial_sequence('order_tracking_history', 'id'), COALESCE((SELECT MAX(id) FROM order_tracking_history), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            END $$;"
        };

        foreach (var sql in ddlStatements)
        {
            try
            {
                db.Database.ExecuteSqlRaw(sql);
            }
            catch (Exception)
            {
                // Silently ignore DDL notes if columns/tables already exist
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Neon PostgreSQL database init note: {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });

    // Only redirect if an HTTPS port is configured (avoids warning when running HTTP-only or behind SSL-terminating proxies)
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTPS_PORT")) || 
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")) ||
        builder.Configuration.GetValue<int?>("https_port") != null)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
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
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self' https: data: wss: ws: 'unsafe-inline' 'unsafe-eval'; connect-src 'self' https: wss: ws: data:;";
        return Task.CompletedTask;
    });
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Strict URL Route Guard & Navigation Security
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
    var userRole = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

    // 1. Admin Route Guard: Only /admin direct URL navigation is routed to Admin Portal (requires Admin role)
    if (path.StartsWith("/admin"))
    {
        if (!isAuthenticated)
        {
            var returnUrl = System.Net.WebUtility.UrlEncode(context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/Account/SignIn?returnUrl={returnUrl}");
            return;
        }
        else if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Home/Restricted");
            return;
        }
    }

    // 2. Protected Customer Account Route Guard: Direct URL hopping to private account sections requires login
    var protectedAccountPaths = new[] { "/account/myaccount", "/account/orders", "/account/wishlist", "/account/addresses", "/account/profile" };
    if (protectedAccountPaths.Any(p => path.StartsWith(p)) && !isAuthenticated)
    {
        var returnUrl = System.Net.WebUtility.UrlEncode(context.Request.Path + context.Request.QueryString);
        context.Response.Redirect($"/Account/SignIn?returnUrl={returnUrl}");
        return;
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
