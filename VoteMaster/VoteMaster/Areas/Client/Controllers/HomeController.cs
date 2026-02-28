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
            var archivedPolls = await _polls.GetArchivedPollsAsync();
            
            // Get polls where user has voted
            var userVotedPolls = new List<(VoteMaster.Models.Poll poll, int voteCount)>();
            
            foreach (var poll in archivedPolls)
            {
                var voteCount = await _polls.GetUserVoteCountAsync(poll.Id, userId);
                if (voteCount > 0)
                {
                    userVotedPolls.Add((poll, voteCount));
                }
            }
            
            return View(userVotedPolls);
        }
    }
}
