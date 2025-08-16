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

        [HttpGet]
        [Route("Client/Vote/{pollId:int}")]
        public async Task<IActionResult> Index(int pollId)
        {
            var poll = await _polls.GetPollAsync(pollId);
            if (poll is null) return NotFound();
            return View(poll);
        }

        [HttpPost]
        [Route("Client/Vote/Cast")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cast(int pollId, int[] optionIds)
        {
            try
            {
                var poll = await _polls.GetPollAsync(pollId);
                if (poll == null) return NotFound();

                if (optionIds == null || !optionIds.Any())
                {
                    TempData["Error"] = "Please select at least one option";
                    return RedirectToAction(nameof(Index), new { pollId });
                }

                if (optionIds.Length < poll.MinVotesPerVoter || optionIds.Length > poll.MaxVotesPerVoter)
                {
                    TempData["Error"] = $"Please select between {poll.MinVotesPerVoter} and {poll.MaxVotesPerVoter} options";
                    return RedirectToAction(nameof(Index), new { pollId });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                foreach (var optionId in optionIds)
                {
                    try
                    {
                        await _polls.CastVoteAsync(optionId, userId);
                    }
                    catch (InvalidOperationException ex)
                    {
                        TempData["Error"] = ex.Message;
                        return RedirectToAction(nameof(Index), new { pollId });
                    }
                }

                return RedirectToAction(nameof(Thanks), new { pollId });
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while casting your votes";
                return RedirectToAction(nameof(Index), new { pollId });
            }
        }

        [Route("Client/Vote/Thanks/{pollId:int}")]
        public IActionResult Thanks(int pollId) => View(model: pollId);
    }
}
