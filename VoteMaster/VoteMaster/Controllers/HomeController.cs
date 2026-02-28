using Microsoft.AspNetCore.Mvc;

namespace VoteMaster.Controllers
{
    public class HomeController : Controller
    {
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
    }
}
