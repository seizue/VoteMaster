using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoteMaster.Services;

namespace VoteMaster.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IPollService _polls;
        public HomeController(IPollService polls) { _polls = polls; }

        public async Task<IActionResult> Index() => View(await _polls.GetActivePollsAsync());
    }
}
