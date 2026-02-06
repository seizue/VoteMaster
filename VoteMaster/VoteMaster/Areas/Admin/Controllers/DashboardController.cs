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

        public async Task<IActionResult> Index(string status = "all")
        {
            var polls = await _polls.GetPollsAsync(status);
            var pollDtos = polls.Select(p => new PollViewDto 
            { 
                Poll = p, 
                Status = _polls.GetPollStatus(p) 
            }).ToList();
            
            ViewBag.CurrentStatus = status;
            ViewBag.StatusOptions = new List<string> { "active", "archived", "upcoming", "all" };
            return View(pollDtos);
        }

        [HttpGet]
        public async Task<IActionResult> ExportResults(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Option,Votes,WeightedScore");
            foreach (var o in poll.Options)
            {
                var votes = o.Votes.Count;
                var weighted = o.Votes.Sum(v => v.User?.Weight ?? 1);
                // escape double quotes in option text
                var text = o.Text?.Replace("\"", "\"\"") ?? "";
                sb.AppendLine($"\"{text}\",{votes},{weighted}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"poll-{poll.Id}-results-{DateTime.UtcNow:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}
