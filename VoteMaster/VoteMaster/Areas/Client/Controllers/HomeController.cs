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

        public async Task<IActionResult> Index(int page = 1, int pageSize = 12)
        {
            var all = (await _polls.GetActivePollsAsync()).ToList();
            var totalPages = (int)Math.Ceiling(all.Count / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = all.Count;

            return View(all.Skip((page - 1) * pageSize).Take(pageSize));
        }

        public async Task<IActionResult> PastPolls(int page = 1, int pageSize = 10)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var allPolls = await _polls.GetAllPollsAsync();

            var userVotedPolls = new List<(VoteMaster.Models.Poll poll, int voteCount)>();
            foreach (var poll in allPolls)
            {
                var voteCount = await _polls.GetUserVoteCountAsync(poll.Id, userId);
                if (voteCount > 0)
                    userVotedPolls.Add((poll, voteCount));
            }

            userVotedPolls = userVotedPolls.OrderByDescending(p => p.poll.EndTime).ToList();

            var totalPages = (int)Math.Ceiling(userVotedPolls.Count / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = userVotedPolls.Count;

            return View(userVotedPolls.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }
    }
}
