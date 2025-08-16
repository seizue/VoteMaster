using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class DashboardController : Controller
    {
        private readonly IPollService _polls;
        public DashboardController(IPollService polls) { _polls = polls; }

        public async Task<IActionResult> Index()
        {
            var active = await _polls.GetActivePollsAsync();
            return View(active);
        }
    }
}
