using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using VoteMaster.Services;

namespace VoteMaster.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _users;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;
        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        public AccountController(IUserService users, IMemoryCache cache, ILogger<AccountController> logger)
        {
            _users = users;
            _cache = cache;
            _logger = logger;
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
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
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
