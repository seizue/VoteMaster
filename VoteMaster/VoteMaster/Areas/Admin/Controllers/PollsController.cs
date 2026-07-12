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
        private readonly IAppSettingsService _appSettings;

        public PollsController(IPollService polls, IUserService users, IAppSettingsService appSettings)
        {
            _polls = polls;
            _users = users;
            _appSettings = appSettings;
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

            ViewBag.CurrentStatus = status;
            ViewBag.StatusOptions = new List<string> { "active", "archived", "upcoming", "all" };
            ViewBag.NetworkBaseUrl = await _appSettings.GetNetworkBaseUrlAsync();
            ViewBag.RequestBaseUrl = $"{Request.Scheme}://{Request.Host}";
            return View(pollDtos);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var voters = (await _users.GetAllForAdminAsync(adminId))
                             .Where(u => u.Role == "Voter" && !u.IsTestAccount)
                             .OrderBy(u => u.FullName ?? u.Username)
                             .ToList();
            ViewBag.Voters = voters;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var sharedUsers = await _polls.GetSharedUsersAsync(id);
            var allAdmins = await _users.GetAllAsync();
            var currentOwnerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var voters = (await _users.GetAllForAdminAsync(adminId))
                             .Where(u => u.Role == "Voter" && !u.IsTestAccount)
                             .OrderBy(u => u.FullName ?? u.Username)
                             .ToList();

            ViewBag.Poll = poll;
            ViewBag.Voters = voters;
            ViewBag.OptionsCsv = string.Join(", ", poll.Options.Select(o => o.Text.Contains(",") ? $"\"{o.Text}\"" : o.Text));
            ViewBag.StartDateTime = poll.StartTime.ToString("yyyy-MM-ddTHH:mm");
            ViewBag.EndDateTime = poll.EndTime.ToString("yyyy-MM-ddTHH:mm");
            ViewBag.SharedUsers = sharedUsers;
            ViewBag.AllAdmins = allAdmins.Where(u => u.Role == "Admin" && u.Id != currentOwnerId).ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string title, string? description, bool allowPublicResults, string optionsCsv,
            string? startDateTime, string? endDateTime, int maxVotesPerVoter = 1, int minVotesPerVoter = 1,
            bool enableLiveVoteCount = false, bool enablePollNotifications = false, bool allowUsercodeEntry = false,
            bool requireAttendance = false)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var options = ParseCsv(optionsCsv);

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
            poll.AllowUsercodeEntry = allowUsercodeEntry;
            poll.RequireAttendance = requireAttendance;

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
            bool enableLiveVoteCount = false, bool enablePollNotifications = false, bool allowUsercodeEntry = false,
            bool requireAttendance = false)
        {
            var options = ParseCsv(optionsCsv);

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

                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var voters = (await _users.GetAllForAdminAsync(adminId))
                                 .Where(u => u.Role == "Voter" && !u.IsTestAccount)
                                 .OrderBy(u => u.FullName ?? u.Username)
                                 .ToList();
                ViewBag.Voters = voters;

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
                AllowUsercodeEntry = allowUsercodeEntry,
                RequireAttendance = requireAttendance,
                OwnerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0")
            };
            await _polls.CreatePollAsync(poll, options);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Results(int id)
        {
            ViewBag.PollId = id;
            var poll = await _polls.GetPollAsync(id);
            ViewBag.PollTitle = poll?.Title ?? "Poll Results";
            var res = await _polls.GetWeightedResultsAsync(id);

            if (poll?.RequireAttendance == true)
            {
                ViewBag.RequireAttendance = true;
                ViewBag.PresentWeightTotal = await _polls.GetPresentWeightTotalAsync(id);
                ViewBag.PresentCount = (await _polls.GetPresentUserIdsAsync(id)).Count;
            }
            else
            {
                ViewBag.RequireAttendance = false;
            }

            ViewBag.NetworkBaseUrl = await _appSettings.GetNetworkBaseUrlAsync();
            ViewBag.RequestBaseUrl = $"{Request.Scheme}://{Request.Host}";
            return View(res);
        }

        public async Task<IActionResult> VoterStatus(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var voterStatus = await _polls.GetVoterVotingStatusAsync(id);
            ViewBag.Poll = poll;
            ViewBag.RequireAttendance = poll.RequireAttendance;
            return View(voterStatus);
        }

        public async Task<IActionResult> Nominees(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            ViewBag.Poll = poll;
            ViewBag.NetworkBaseUrl = await _appSettings.GetNetworkBaseUrlAsync();
            ViewBag.RequestBaseUrl = $"{Request.Scheme}://{Request.Host}";
            return View(poll);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> End(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            // Only end polls that are currently active
            if (_polls.GetPollStatus(poll) != "Active")
                return RedirectToAction(nameof(Index));

            poll.EndTime = DateTime.UtcNow;
            await _polls.UpdatePollAsync(poll);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id, string newEndDateTime)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            if (!DateTime.TryParse(newEndDateTime, out var parsedEnd) || parsedEnd <= DateTime.Now)
            {
                TempData["Error"] = "Please provide a valid future end date/time to reactivate the poll.";
                return RedirectToAction(nameof(VoterStatus), new { id });
            }

            await _polls.ReactivatePollAsync(id, parsedEnd);
            TempData["Success"] = $"Poll reactivated. New end time: {parsedEnd:g}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetVotes(int id, string scope, int[]? selectedUserIds)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            if (scope == "all")
            {
                await _polls.ResetAllVotesAsync(id);
                TempData["ResetSuccess"] = "All votes for this poll have been reset.";
            }
            else if (scope == "selected" && selectedUserIds != null && selectedUserIds.Length > 0)
            {
                await _polls.ResetSelectedVotesAsync(id, selectedUserIds);
                TempData["ResetSuccess"] = $"Votes reset for {selectedUserIds.Length} voter(s).";
            }
            else
            {
                TempData["ResetError"] = "No voters selected. Please check at least one voter to reset.";
            }

            return RedirectToAction(nameof(VoterStatus), new { id });
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

        // ── Attendance ────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the attendance management page for a poll — lists all voters
        /// with their present/absent status and lets admin mark them in bulk.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Attendance(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            var adminId      = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var managedVoters = (await _users.GetAllForAdminAsync(adminId))
                                    .Where(u => u.Role == "Voter").ToList();

            var presentIds   = await _polls.GetPresentUserIdsAsync(id);
            var voterStatus  = await _polls.GetVoterVotingStatusAsync(id);
            var votedIds     = voterStatus.Where(v => v.HasVoted).Select(v => v.UserId).ToHashSet();

            // Weight totals for present and absent voters (computed from already-loaded list)
            var presentWeight = managedVoters.Where(u => presentIds.Contains(u.Id)).Sum(u => u.Weight);
            var absentWeight  = managedVoters.Where(u => !presentIds.Contains(u.Id)).Sum(u => u.Weight);

            ViewBag.Poll          = poll;
            ViewBag.PresentIds    = presentIds;
            ViewBag.VotedIds      = votedIds;
            ViewBag.PresentWeight = presentWeight;
            ViewBag.AbsentWeight  = absentWeight;
            return View(managedVoters);
        }

        /// <summary>Mark selected voters as present (or absent).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance(int id, string action, int[]? selectedUserIds)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            if (selectedUserIds == null || selectedUserIds.Length == 0)
            {
                TempData["AttendanceError"] = "No voters selected.";
                return RedirectToAction(nameof(Attendance), new { id });
            }

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            if (action == "present")
            {
                await _polls.MarkPresentAsync(id, selectedUserIds, adminId);
                TempData["AttendanceSuccess"] = $"{selectedUserIds.Length} voter(s) marked as present.";
            }
            else if (action == "absent")
            {
                await _polls.MarkAbsentAsync(id, selectedUserIds);
                TempData["AttendanceSuccess"] = $"{selectedUserIds.Length} voter(s) marked as absent.";
            }

            return RedirectToAction(nameof(Attendance), new { id });
        }

        /// <summary>Mark a single voter present or absent — used by the quick-toggle button.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAttendance(int id, int userId, bool present)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            if (present)
                await _polls.MarkPresentAsync(id, new[] { userId }, adminId);
            else
                await _polls.MarkAbsentAsync(id, new[] { userId });

            return RedirectToAction(nameof(Attendance), new { id });
        }

        // ── Proxy Voting ──────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the proxy voting form: all non-voted voters managed by this admin,
        /// with checkboxes for each poll option.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProxyVote(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            // Only allow proxy voting on active polls
            if (_polls.GetPollStatus(poll) != "Active")
            {
                TempData["Error"] = "Proxy voting is only available for active polls.";
                return RedirectToAction(nameof(Index));
            }

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            // Voters that this admin manages
            var managedVoters = await _users.GetAllForAdminAsync(adminId);
            var voters = managedVoters.Where(u => u.Role == "Voter" && !u.IsTestAccount).ToList();

            // Get who has already voted
            var voterStatus = await _polls.GetVoterVotingStatusAsync(id);
            var votedIds = voterStatus.Where(v => v.HasVoted).Select(v => v.UserId).ToHashSet();

            ViewBag.Poll = poll;
            ViewBag.VotedIds = votedIds;
            return View(voters);
        }

        /// <summary>
        /// Processes proxy vote submissions.
        /// Form fields: userId_[id] (hidden, one per voter) and vote_[userId]_[optionId] (checkboxes).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProxyVote(int id, IFormCollection form)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            if (_polls.GetPollStatus(poll) != "Active")
            {
                TempData["Error"] = "Proxy voting is only available for active polls.";
                return RedirectToAction(nameof(Index));
            }

            // Parse the submitted form into a userId → List<optionId> map
            var userOptionMap = new Dictionary<int, List<int>>();

            // Find all voter id keys (hidden fields: userId_<id>)
            foreach (var key in form.Keys.Where(k => k.StartsWith("userId_")))
            {
                if (int.TryParse(form[key], out var userId))
                {
                    userOptionMap[userId] = new List<int>();
                }
            }

            // Fill in chosen options (checkboxes: vote_<userId>_<optionId>)
            foreach (var key in form.Keys.Where(k => k.StartsWith("vote_")))
            {
                var parts = key.Split('_');
                if (parts.Length == 3
                    && int.TryParse(parts[1], out var userId)
                    && int.TryParse(parts[2], out var optionId)
                    && userOptionMap.ContainsKey(userId))
                {
                    userOptionMap[userId].Add(optionId);
                }
            }

            // Remove voters where admin selected no options (they chose not to proxy-vote that user)
            var toRemove = userOptionMap.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var uid in toRemove) userOptionMap.Remove(uid);

            if (userOptionMap.Count == 0)
            {
                TempData["Error"] = "No votes were submitted. Please select at least one option for at least one voter.";
                return RedirectToAction(nameof(ProxyVote), new { id });
            }

            var result = await _polls.ProxyCastVotesAsync(id, userOptionMap);

            TempData["ProxySuccess"] = $"Successfully cast votes for {result.Processed} voter(s).";
            if (result.Skipped > 0)
            {
                TempData["ProxySkipped"] = $"{result.Skipped} voter(s) were skipped (already voted or invalid selection): {string.Join(", ", result.SkippedUsernames)}.";
            }

            return RedirectToAction(nameof(VoterStatus), new { id });
        }

        private string[] ParseCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
            var list = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    var val = current.ToString().Trim(' ', '"', '\t', '\r', '\n');
                    if (!string.IsNullOrEmpty(val)) list.Add(val);
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            var lastVal = current.ToString().Trim(' ', '"', '\t', '\r', '\n');
            if (!string.IsNullOrEmpty(lastVal)) list.Add(lastVal);
            return list.ToArray();
        }
    }
}
