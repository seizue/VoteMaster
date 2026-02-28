using Microsoft.AspNetCore.Mvc;

namespace VoteMaster.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return User.IsInRole("Admin")
                    ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
                    : RedirectToAction("Index", "Home", new { area = "Client" });
            }
            return View("Landing");
        }
        
        public IActionResult Landing()
        {
            return View();
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            _logger.LogInformation("Health check endpoint called");
            var hasConnectionString = !string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection"));
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                hasConnectionString = hasConnectionString
            });
        }
    }
}
