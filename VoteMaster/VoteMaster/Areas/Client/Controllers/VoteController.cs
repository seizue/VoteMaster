using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using VoteMaster.Hubs;
using VoteMaster.Services;

namespace VoteMaster.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class VoteController : Controller
    {
        private readonly IPollService _polls;
        private readonly IHubContext<ResultsHub> _hubContext;
        
        public VoteController(IPollService polls, IHubContext<ResultsHub> hubContext) 
        { 
            _polls = polls;
            _hubContext = hubContext;
        }

        [HttpGet]
        [Route("Client/Vote/{pollId:int}")]
        public async Task<IActionResult> Index(int pollId)
        {
            var poll = await _polls.GetPollAsync(pollId);
            if (poll is null) return NotFound();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var hasVoted = await _polls.HasUserVotedAsync(pollId, userId);
            var voteCount = await _polls.GetUserVoteCountAsync(pollId, userId);
            
            // Get user's votes if they have voted
            var userVotes = new List<int>();
            if (hasVoted)
            {
                var votes = await _polls.GetUserVotesForPollAsync(pollId, userId);
                userVotes = votes;
            }

            // Check if poll has ended
            var pollStatus = _polls.GetPollStatus(poll);
            var showResults = pollStatus == "Archived" || (hasVoted && poll.AllowPublicResults);

            ViewBag.HasVoted = hasVoted;
            ViewBag.VoteCount = voteCount;
            ViewBag.MaxVotes = poll.MaxVotesPerVoter;
            ViewBag.UserVotes = userVotes;
            ViewBag.ShowResults = showResults;
            ViewBag.PollStatus = pollStatus;

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
                        
                        // Send real-time update if enabled
                        if (poll.EnableLiveVoteCount)
                        {
                            var option = poll.Options.FirstOrDefault(o => o.Id == optionId);
                            if (option != null)
                            {
                                var newVoteCount = option.Votes.Count + 1;
                                await _hubContext.Clients.Group($"Poll_{pollId}")
                                    .SendAsync("VoteUpdated", optionId, newVoteCount);
                            }
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        TempData["Error"] = ex.Message;
                        return RedirectToAction(nameof(Index), new { pollId });
                    }
                }

                // Update voter participation
                var voterStatus = await _polls.GetVoterVotingStatusAsync(pollId);
                var totalVoters = voterStatus.Count;
                var votedCount = voterStatus.Count(v => v.HasVoted);
                await _hubContext.Clients.Group($"Poll_{pollId}")
                    .SendAsync("VoterParticipationUpdated", totalVoters, votedCount);

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

        [HttpPost]
        [Route("Client/Vote/Reset")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset(int pollId)
        {
            try
            {
                var poll = await _polls.GetPollAsync(pollId);
                if (poll == null) return NotFound();

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                
                await _polls.ResetUserVotesAsync(pollId, userId);
                
                TempData["Success"] = "Your votes have been reset successfully. You can vote again.";
                return RedirectToAction(nameof(Index), new { pollId });
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while resetting your votes";
                return RedirectToAction(nameof(Index), new { pollId });
            }
        }
    }
}
