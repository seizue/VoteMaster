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

        public async Task<IActionResult> Index(string status = "active")
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
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(string title, string? description, bool allowPublicResults, string optionsCsv, 
            string? startDateTime, string? endDateTime, int maxVotesPerVoter = 1, int minVotesPerVoter = 1)
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
                EndTime = endTime
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
    }
}
