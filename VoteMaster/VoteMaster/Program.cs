using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VoteMaster.Data;
using VoteMaster.Services;
using VoteMaster.Hubs;
using VoteMaster.Models; 
using Microsoft.AspNetCore.Identity; 
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ===== Enhanced Logging for Azure =====
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

// ===== Rate Limiting Configuration =====
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Login endpoint rate limit (stricter) - Protects against brute force
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // API rate limit - Protects API from abuse
    options.AddSlidingWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 3;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // Voting endpoint rate limit - Prevents rapid-fire voting attempts
    options.AddFixedWindowLimiter("voting", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/json";
        
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            message = "You have exceeded the rate limit. Please slow down and try again later.",
            retryAfter = "60 seconds"
        }, cancellationToken: token);
    };
});
 
// ===== Services =====
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// ===== Anti-Forgery Token Configuration =====
builder.Services.AddAntiforgery(options =>
{
    // Allow HTTP in development or when not using HTTPS
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
});

// ===== Memory Cache for Login Attempts =====
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// ===== Session Configuration =====
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout after 30 minutes of inactivity
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

builder.Services.AddScoped<IPollService, PollService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// ===== Enhanced Authentication Configuration =====
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Cookie expires after 8 hours
        options.SlidingExpiration = true; // Renew cookie on activity
        options.Cookie.HttpOnly = true; // Prevent XSS attacks
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Allow HTTP and HTTPS
        options.Cookie.SameSite = SameSiteMode.Lax; // CSRF protection (Lax for better compatibility)
        options.Cookie.Name = "VoteMaster.Auth";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ===== Security Headers =====
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

var app = builder.Build();

// ======== SEED DATABASE WITH ADMIN & USERS ========
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Starting database migration...");
        logger.LogInformation($"Connection string configured: {!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("DefaultConnection"))}");
        
        db.Database.Migrate(); // Apply migrations
        logger.LogInformation("Database migration completed successfully.");

        var config = app.Configuration.GetSection("Seed");
        var passwordHasher = new PasswordHasher<AppUser>();
        bool saveRequired = false;

        // Seed admin
        var adminUsername = config["Admin:Username"] ?? "admin";
        var adminPassword = config["Admin:Password"] ?? "admin123";
        var adminWeight = int.Parse(config["Admin:Weight"] ?? "1");

        var adminUser = db.Users.FirstOrDefault(u => u.Username == adminUsername);
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                Username = adminUsername,
                Role = "Admin",
                Weight = adminWeight
            };
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, adminPassword);
            db.Users.Add(adminUser);
            saveRequired = true;
            logger.LogInformation("Admin user will be created.");
        }

        // Seed normal users
        var seedUsers = config.GetSection("Users").Get<List<UserSeedModel>>() ?? new List<UserSeedModel>();
        foreach (var u in seedUsers)
        {
            if (!db.Users.Any(x => x.Username == u.Username))
            {
                var newUser = new AppUser
                {
                    Username = u.Username,
                    Role = u.Role,
                    Weight = u.Weight
                };
                newUser.PasswordHash = passwordHasher.HashPassword(newUser, u.Password);
                db.Users.Add(newUser);
                saveRequired = true;
            }
        }

        if (saveRequired)
        {
            db.SaveChanges();
            logger.LogInformation("Database seeding completed successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization. Application will continue but database may not be ready.");
        // Don't throw - let the app start even if DB fails
    }
}

// ========= MIDDLEWARE =========
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter(); // Enable rate limiting
app.UseSession(); // Enable session
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ResultsHub>("/hubs/results");

// Areas (Admin/Client)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// API
app.MapControllers();

// Default
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ====== Helper Seed Model ======
public class UserSeedModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Weight { get; set; }
    public string Role { get; set; } = string.Empty;
}
