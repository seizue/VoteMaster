using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VoteMaster.Models;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class PollsController : Controller
    {
        private readonly IPollService _polls;
        private readonly IUserService _users;
        public PollsController(IPollService polls, IUserService users) { _polls = polls; _users = users; }

        public async Task<IActionResult> Index(string status = "active")
        {
            var ownerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var polls = await _polls.GetPollsForOwnerAsync(status, ownerId);
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
        public IActionResult Create() => View();

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var sharedUsers = await _polls.GetSharedUsersAsync(id);
            var allAdmins = await _users.GetAllAsync();
            var currentOwnerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            ViewBag.Poll = poll;
            ViewBag.OptionsCsv = string.Join(", ", poll.Options.Select(o => o.Text));
            ViewBag.StartDateTime = poll.StartTime.ToString("yyyy-MM-ddTHH:mm");
            ViewBag.EndDateTime = poll.EndTime.ToString("yyyy-MM-ddTHH:mm");
            ViewBag.SharedUsers = sharedUsers;
            ViewBag.AllAdmins = allAdmins.Where(u => u.Role == "Admin" && u.Id != currentOwnerId).ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string title, string? description, bool allowPublicResults, string optionsCsv,
            string? startDateTime, string? endDateTime, int maxVotesPerVoter = 1, int minVotesPerVoter = 1,
            bool enableLiveVoteCount = false, bool enablePollNotifications = false)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var options = (optionsCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Parse datetime inputs - convert from local time to UTC
            DateTime startTime = poll.StartTime;
            DateTime endTime = poll.EndTime;

            if (!string.IsNullOrEmpty(startDateTime) && DateTime.TryParse(startDateTime, out var parsedStart))
            {
                startTime = parsedStart.ToUniversalTime();
            }

            if (!string.IsNullOrEmpty(endDateTime) && DateTime.TryParse(endDateTime, out var parsedEnd))
            {
                endTime = parsedEnd.ToUniversalTime();
            }

            // Basic validation for min/max votes
            if (minVotesPerVoter < 1)
            {
                ModelState.AddModelError("minVotesPerVoter", "Minimum votes must be at least 1.");
            }
            if (maxVotesPerVoter < minVotesPerVoter)
            {
                ModelState.AddModelError("maxVotesPerVoter", "Maximum votes must be greater than or equal to minimum votes.");
            }
            if (maxVotesPerVoter > options.Length)
            {
                ModelState.AddModelError("maxVotesPerVoter", "Maximum votes cannot exceed the number of options.");
            }
            if (startTime >= endTime)
            {
                ModelState.AddModelError("endDateTime", "End time must be after start time.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Poll = poll;
                ViewBag.Title = title;
                ViewBag.Description = description;
                ViewBag.OptionsCsv = optionsCsv;
                ViewBag.MaxVotes = maxVotesPerVoter;
                ViewBag.MinVotes = minVotesPerVoter;
                ViewBag.AllowPublicResults = allowPublicResults;
                ViewBag.StartDateTime = startDateTime;
                ViewBag.EndDateTime = endDateTime;
                return View();
            }

            // Update poll properties
            poll.Title = title;
            poll.Description = description;
            poll.AllowPublicResults = allowPublicResults;
            poll.MaxVotesPerVoter = maxVotesPerVoter;
            poll.MinVotesPerVoter = minVotesPerVoter;
            poll.StartTime = startTime;
            poll.EndTime = endTime;
            poll.EnableLiveVoteCount = enableLiveVoteCount;
            poll.EnablePollNotifications = enablePollNotifications;

            // Update options - clear and recreate
            poll.Options.Clear();
            foreach (var text in options)
            {
                poll.Options.Add(new PollOption { Text = text });
            }

            await _polls.UpdatePollAsync(poll);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Create(string title, string? description, bool allowPublicResults, string optionsCsv, 
            string? startDateTime, string? endDateTime, int maxVotesPerVoter = 1, int minVotesPerVoter = 1,
            bool enableLiveVoteCount = false, bool enablePollNotifications = false)
        {
            var options = (optionsCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Parse datetime inputs - convert from local time to UTC
            DateTime startTime = DateTime.UtcNow;
            DateTime endTime = DateTime.UtcNow.AddHours(2);

            if (!string.IsNullOrEmpty(startDateTime) && DateTime.TryParse(startDateTime, out var parsedStart))
            {
                // The datetime-local input gives us local time, convert to UTC
                startTime = parsedStart.ToUniversalTime();
            }

            if (!string.IsNullOrEmpty(endDateTime) && DateTime.TryParse(endDateTime, out var parsedEnd))
            {
                // The datetime-local input gives us local time, convert to UTC
                endTime = parsedEnd.ToUniversalTime();
            }

            // Basic validation for min/max votes
            if (minVotesPerVoter < 1)
            {
                ModelState.AddModelError("minVotesPerVoter", "Minimum votes must be at least 1.");
            }
            if (maxVotesPerVoter < minVotesPerVoter)
            {
                ModelState.AddModelError("maxVotesPerVoter", "Maximum votes must be greater than or equal to minimum votes.");
            }
            if (maxVotesPerVoter > options.Length)
            {
                ModelState.AddModelError("maxVotesPerVoter", "Maximum votes cannot exceed the number of options.");
            }
            if (startTime >= endTime)
            {
                ModelState.AddModelError("endDateTime", "End time must be after start time.");
            }

            if (!ModelState.IsValid)
            {
                // Preserve entered values so the admin doesn't lose them
                ViewBag.Title = title;
                ViewBag.Description = description;
                ViewBag.OptionsCsv = optionsCsv;
                ViewBag.MaxVotes = maxVotesPerVoter;
                ViewBag.MinVotes = minVotesPerVoter;
                ViewBag.AllowPublicResults = allowPublicResults;
                ViewBag.StartDateTime = startDateTime;
                ViewBag.EndDateTime = endDateTime;
                return View();
            }

            var poll = new Poll 
            { 
                Title = title, 
                Description = description, 
                AllowPublicResults = allowPublicResults,
                MaxVotesPerVoter = maxVotesPerVoter, 
                MinVotesPerVoter = minVotesPerVoter,
                StartTime = startTime,
                EndTime = endTime,
                EnableLiveVoteCount = enableLiveVoteCount,
                EnablePollNotifications = enablePollNotifications,
                OwnerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0")
            };
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Share(int pollId, int withUserId)
        {
            await _polls.SharePollAsync(pollId, withUserId);
            return RedirectToAction(nameof(Edit), new { id = pollId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unshare(int pollId, int withUserId)
        {
            await _polls.UnsharePollAsync(pollId, withUserId);
            return RedirectToAction(nameof(Edit), new { id = pollId });
        }
    }
}
