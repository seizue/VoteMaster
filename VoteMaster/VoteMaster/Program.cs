using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VoteMaster.Data;
using VoteMaster.Services;
using VoteMaster.Hubs;
using VoteMaster.Models; 
using Microsoft.AspNetCore.Identity; 
using System.Linq;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// ===== Enhanced Logging for Azure =====
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

// Log startup information
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Startup");
logger.LogInformation("Application starting...");
logger.LogInformation($"Environment: {builder.Environment.EnvironmentName}");

// ===== Services =====
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
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
