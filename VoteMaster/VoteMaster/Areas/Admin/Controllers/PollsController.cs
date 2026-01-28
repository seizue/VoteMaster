using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoteMaster.Models;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class PollsController : Controller
    {
        private readonly IPollService _polls;
        public PollsController(IPollService polls) { _polls = polls; }

        public async Task<IActionResult> Index() => View(await _polls.GetActivePollsAsync());

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(string title, string? description, bool allowPublicResults, string optionsCsv)
        {
            var options = (optionsCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var poll = new Poll { Title = title, Description = description, AllowPublicResults = allowPublicResults };
            await _polls.CreatePollAsync(poll, options);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Results(int id)
        {
            ViewBag.PollId = id;
            var res = await _polls.GetWeightedResultsAsync(id);
            return View(res);
        }

        public async Task<IActionResult> VoterStatus(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var voterStatus = await _polls.GetVoterVotingStatusAsync(id);
            ViewBag.Poll = poll;
            return View(voterStatus);
        }

       [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _polls.DeletePollAsync(id);
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
    }
}
