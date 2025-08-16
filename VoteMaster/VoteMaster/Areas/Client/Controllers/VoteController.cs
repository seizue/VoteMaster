using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VoteMaster.Services;

namespace VoteMaster.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class VoteController : Controller
    {
        private readonly IPollService _polls;
        public VoteController(IPollService polls) { _polls = polls; }

        [HttpGet("{area}/Vote/{pollId:int}")]
        public async Task<IActionResult> Index(int pollId)
        {
            var poll = await _polls.GetPollAsync(pollId);
            if (poll is null) return NotFound();
            return View(poll);
        }

        [HttpPost]
        public async Task<IActionResult> Cast(int optionId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _polls.CastVoteAsync(optionId, userId);
          if (!int.TryParse(Request.Form["PollId"], out var pollId))
{
    return BadRequest("Invalid PollId");
}

            return RedirectToAction("Thanks", new { pollId });
        }

        public IActionResult Thanks(int pollId) => View(model: pollId);
    }
}
