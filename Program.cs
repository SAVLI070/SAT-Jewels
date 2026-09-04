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
builder.Services.AddScoped<SAT1.BAL.EmailNotificationService>();
builder.Services.AddScoped<SAT1.BAL.Shipping.IShippingProviderService, SAT1.BAL.Shipping.DefaultShippingProviderService>();
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
            @"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""RawPayload"" text;"
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
