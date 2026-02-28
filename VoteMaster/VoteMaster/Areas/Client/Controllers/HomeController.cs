using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

        public async Task<IActionResult> PastPolls()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            // Get all polls with their options and votes loaded
            var allPolls = await _polls.GetAllPollsAsync();
            
            // Get all polls where user has voted
            var userVotedPolls = new List<(VoteMaster.Models.Poll poll, int voteCount)>();
            
            foreach (var poll in allPolls)
            {
                var voteCount = await _polls.GetUserVoteCountAsync(poll.Id, userId);
                if (voteCount > 0)
                {
                    userVotedPolls.Add((poll, voteCount));
                }
            }
            
            // Sort by end time descending (most recent first)
            userVotedPolls = userVotedPolls.OrderByDescending(p => p.poll.EndTime).ToList();
            
            // Debug: Log the count
            ViewBag.TotalPolls = allPolls.Count;
            ViewBag.UserVotedCount = userVotedPolls.Count;
            
            return View(userVotedPolls);
        }
    }
}
