using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using VoteMaster.Models;
using VoteMaster.Services;

namespace VoteMaster.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _users;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _config;
        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        public AccountController(IUserService users, IMemoryCache cache, ILogger<AccountController> logger, IConfiguration config)
        {
            _users = users;
            _cache = cache;
            _logger = logger;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var ipAddress = GetClientIpAddress();
            var lockoutKey = $"lockout_{ipAddress}";
            
            if (_cache.TryGetValue(lockoutKey, out DateTime lockoutEnd))
            {
                var remainingTime = lockoutEnd - DateTime.UtcNow;
                if (remainingTime > TimeSpan.Zero)
                {
                    ViewBag.Error = $"Account temporarily locked. Please try again in {remainingTime.Minutes} minutes and {remainingTime.Seconds} seconds.";
                    ViewBag.IsLocked = true;
                }
            }
            
            return View(model: returnUrl);
        }

        [HttpPost]
        [EnableRateLimiting("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null, string? intent = null)
        {
            var ipAddress = GetClientIpAddress();
            var attemptKey = $"attempts_{ipAddress}_{username}";
            var lockoutKey = $"lockout_{ipAddress}";

            // Check if account is locked
            if (_cache.TryGetValue(lockoutKey, out DateTime lockoutEnd))
            {
                var remainingTime = lockoutEnd - DateTime.UtcNow;
                if (remainingTime > TimeSpan.Zero)
                {
                    _logger.LogWarning($"Login attempt from locked IP: {ipAddress} for user: {username}");
                    ViewBag.Error = $"Too many failed attempts. Account locked for {remainingTime.Minutes} minutes and {remainingTime.Seconds} seconds.";
                    ViewBag.IsLocked = true;
                    return View(model: returnUrl);
                }
                else
                {
                    // Lockout expired, remove it
                    _cache.Remove(lockoutKey);
                    _cache.Remove(attemptKey);
                }
            }

            // Get current attempt count
            var attempts = _cache.GetOrCreate(attemptKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                return 0;
            });

            var user = await _users.AuthenticateAsync(username, password);
            if (user is null)
            {
                attempts++;
                _cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(15));

                var remainingAttempts = MaxLoginAttempts - attempts;
                
                _logger.LogWarning($"Failed login attempt {attempts}/{MaxLoginAttempts} from IP: {ipAddress} for user: {username}");

                if (attempts >= MaxLoginAttempts)
                {
                    // Lock the account
                    var lockoutEndTime = DateTime.UtcNow.Add(LockoutDuration);
                    _cache.Set(lockoutKey, lockoutEndTime, LockoutDuration);
                    
                    _logger.LogWarning($"Account locked for IP: {ipAddress} after {MaxLoginAttempts} failed attempts");
                    
                    ViewBag.Error = $"Too many failed login attempts. Your account has been temporarily locked for {LockoutDuration.Minutes} minutes.";
                    ViewBag.IsLocked = true;
                }
                else
                {
                    ViewBag.Error = $"Invalid credentials. {remainingAttempts} attempt(s) remaining before account lockout.";
                }
                
                return View(model: returnUrl);
            }

            // Successful login - clear attempts
            _cache.Remove(attemptKey);
            _cache.Remove(lockoutKey);

            // Enforce tab intent — a Voter cannot sign in via the Admin tab and vice versa
            if (intent == "admin" && user.Role != "Admin")
            {
                ViewBag.Error = "Access denied. This login is for admins only.";
                ViewBag.Intent = "admin";
                return View(model: returnUrl);
            }
            if (intent == "voter" && user.Role != "Voter")
            {
                ViewBag.Error = "Access denied. Use the Admin tab to sign in.";
                ViewBag.Intent = "voter";
                return View(model: returnUrl);
            }
            
            _logger.LogInformation($"Successful login for user: {username} from IP: {ipAddress}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("LoginTime", DateTime.UtcNow.ToString("o")),
                new Claim("IpAddress", ipAddress)
            };
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false, // Session cookie
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            };
            
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                principal, 
                authProperties);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return user.Role == "Admin"
                ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
                : RedirectToAction("Index", "Home", new { area = "Client" });
        }

        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name ?? "Unknown";
            var ipAddress = GetClientIpAddress();
            
            _logger.LogInformation($"User logged out: {username} from IP: {ipAddress}");
            
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            
            return RedirectToAction("Login");
        }

        // ── Google OAuth ──
        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null, string? intent = null)
        {
            var clientId = _config["Authentication:Google:ClientId"];

            if (string.IsNullOrWhiteSpace(clientId))
            {
                TempData["Error"] = "Google sign-in is not configured yet. Please contact the administrator.";
                return RedirectToAction(nameof(Login));
            }

            var redirectUrl = Url.Action(nameof(GoogleCallback), "Account", new { returnUrl, intent });
            var properties  = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, "Google");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string? returnUrl = null, string? remoteError = null, string? intent = null)
        {
            try
            {
                if (remoteError != null)
                {
                    _logger.LogWarning("Google OAuth remote error: {Error}", remoteError);
                    TempData["Error"] = $"Google sign-in error: {remoteError}";
                    return RedirectToAction(nameof(Login));
                }

                var result = await HttpContext.AuthenticateAsync(
                    Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme);

                _logger.LogInformation("Google callback: Succeeded={S}, Principal={P}",
                    result?.Succeeded, result?.Principal?.Identity?.Name);

                if (result?.Principal == null || !result.Succeeded)
                {
                    TempData["Error"] = "Google authentication failed. Please try again.";
                    return RedirectToAction(nameof(Login));
                }

                var email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
                var name  = result.Principal.FindFirstValue(ClaimTypes.Name)  ?? "";

                _logger.LogInformation("Google callback: email={Email} name={Name}", email, name);

                // Email is the only trusted identity from Google — never match by username
                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["Error"] = "Could not retrieve your Google email. Please try again.";
                    return RedirectToAction(nameof(Login));
                }

                // Look up solely by email to prevent account hijacking via name collision
                var existing = await _users.GetByEmailAsync(email);

                if (existing is null)
                {
                    // Derive a username from display name or email prefix, deduplicate if taken
                    var baseUsername = (string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name)
                                          .Replace(" ", "").ToLowerInvariant();
                    var username = baseUsername;
                    var suffix = 1;
                    while (await _users.GetByUsernameAsync(username) != null)
                        username = $"{baseUsername}{suffix++}";

                    var assignedRole = (intent == "admin") ? "Admin" : "Voter";
                    var newUser = new AppUser
                    {
                        Username     = username,
                        Email        = email,
                        Role         = assignedRole,
                        Weight       = 1,
                        PasswordHash = ""
                    };
                    existing = await _users.CreateAsync(newUser, Guid.NewGuid().ToString());
                    existing.CreatedByAdminId = existing.Id;
                    await _users.UpdateAsync(existing);
                    _logger.LogInformation("Auto-registered new {Role} via Google: {Username} ({Email})", assignedRole, username, email);
                }
                else if (string.IsNullOrWhiteSpace(existing.Email))
                {
                    // Should never happen since we matched by email, but guard anyway
                    existing.Email = email;
                    await _users.UpdateAsync(existing);
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, existing.Id.ToString()),
                    new Claim(ClaimTypes.Name, existing.Username),
                    new Claim(ClaimTypes.Role, existing.Role),
                    new Claim("LoginTime", DateTime.UtcNow.ToString("o")),
                    new Claim("Email", email)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return existing.Role == "Admin"
                    ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
                    : RedirectToAction("Index", "Home", new { area = "Client" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in GoogleCallback");
                TempData["Error"] = $"Sign-in failed: {ex.Message}";
                return RedirectToAction(nameof(Login));
            }
        }

        public IActionResult Denied() => View();

        private string GetClientIpAddress()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            
            // Check for forwarded IP (when behind proxy/load balancer)
            if (HttpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            }
            
            return ipAddress ?? "unknown";
        }
    }
}
