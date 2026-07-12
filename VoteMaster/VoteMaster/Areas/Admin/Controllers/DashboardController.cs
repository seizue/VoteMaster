using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VoteMaster.Data;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class DashboardController : Controller
    {
        private readonly IPollService _polls;
        private readonly IAppSettingsService _appSettings;
        private readonly AppDbContext _db;

        public DashboardController(IPollService polls, IAppSettingsService appSettings, AppDbContext db)
        {
            _polls = polls;
            _appSettings = appSettings;
            _db = db;
        }

        public async Task<IActionResult> Index(string status = "active")
        {
            var ownerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var polls = await _polls.GetPollsForOwnerAsync(status, ownerId);
            var pollDtos = polls.Select(p => new PollViewDto
            {
                Poll = p,
                Status = _polls.GetPollStatus(p)
            }).ToList();

            // KPI stats
            var allOwnedPolls = await _polls.GetPollsForOwnerAsync("all", ownerId);
            ViewBag.TotalPollsCount = allOwnedPolls.Count;
            ViewBag.ActivePollsCount = allOwnedPolls.Count(p => _polls.GetPollStatus(p) == "Active");
            ViewBag.TotalVotersCount = await _db.Users.CountAsync(u => u.Role == "Voter" && u.CreatedByAdminId == ownerId);
            var ownedPollIds = allOwnedPolls.Select(p => p.Id).ToList();
            ViewBag.TotalTemplatesCount = await _db.TicketTemplates.CountAsync(t => ownedPollIds.Contains(t.PollId));

            // Recent activity feed — last 10 events across votes, poll creation, voter registration
            // Recent activity feed — fetch enough items for client-side pagination
            var recentVotes = await _db.Votes
                .Include(v => v.User)
                .Include(v => v.Option).ThenInclude(o => o.Poll)
                .Where(v => ownedPollIds.Contains(v.Option.PollId))
                .OrderByDescending(v => v.VotedAt)
                .Take(50)
                .Select(v => new ActivityItem
                {
                    Kind = "vote",
                    Description = $"{v.User.Username} voted in \"{v.Option.Poll.Title}\"",
                    When = v.VotedAt,
                    HasTimestamp = true
                })
                .ToListAsync();

            var recentPolls = allOwnedPolls
                .OrderByDescending(p => p.StartTime)
                .Take(20)
                .Select(p => new ActivityItem
                {
                    Kind = "poll",
                    Description = $"Poll created: \"{p.Title}\"",
                    When = p.StartTime,
                    HasTimestamp = true
                });

            var voterData = await _db.Users
                .Where(u => u.Role == "Voter" && u.CreatedByAdminId == ownerId)
                .OrderByDescending(u => u.Id)
                .Take(20)
                .Select(u => new { u.Username, u.FullName })
                .ToListAsync();

            var recentVoters = voterData.Select(u => new ActivityItem
            {
                Kind = "voter",
                Description = "Voter registered: " + u.Username + (u.FullName != null ? " (" + u.FullName + ")" : ""),
                When = DateTime.MinValue,
                HasTimestamp = false
            }).ToList();

            // Timestamped events sorted newest-first, voters appended after
            var allActivity = recentVotes
                .Concat(recentPolls)
                .OrderByDescending(a => a.When)
                .Concat(recentVoters)
                .ToList();

            ViewBag.AllActivityItems = allActivity;

            ViewBag.CurrentStatus = status;
            ViewBag.StatusOptions = new List<string> { "active", "archived", "upcoming", "all" };
            ViewBag.NetworkBaseUrl = await _appSettings.GetNetworkBaseUrlAsync();
            ViewBag.RequestBaseUrl = $"{Request.Scheme}://{Request.Host}";
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
                var text = o.Text?.Replace("\"", "\"\"") ?? "";
                sb.AppendLine($"\"{text}\",{votes},{weighted}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"poll-{poll.Id}-results-{DateTime.UtcNow:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }

    public class ActivityItem
    {
        public string Kind { get; set; } = string.Empty;       // "vote", "poll", "voter"
        public string Description { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool HasTimestamp { get; set; } = true;
    }
}
