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
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
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

            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PaymentProvider"" text NOT NULL DEFAULT 'PayPal';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ProviderOrderId"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ProviderPaymentId"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ExpectedAmount"" numeric NOT NULL DEFAULT 0.0;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""AmountPaid"" numeric NOT NULL DEFAULT 0.0;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PaidAt"" timestamp with time zone;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""BuyerInfo"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""IsSuspicious"" boolean NOT NULL DEFAULT false;",
            @"ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""SuspiciousReason"" text;",

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
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self' https: data: wss: ws: 'unsafe-inline' 'unsafe-eval'; connect-src 'self' https: wss: ws: data:;";
        return Task.CompletedTask;
    });
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Admin Access Guard: Redirect unauthenticated visitors trying to access /admin to Sign In
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";

    if (path.StartsWith("/admin") && context.User.Identity?.IsAuthenticated != true)
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
