using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using VoteMaster.Hubs;
using VoteMaster.Models;
using VoteMaster.Services;

namespace VoteMaster.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize]
    public class VoteController : Controller
    {
        private readonly IPollService _polls;
        private readonly IHubContext<ResultsHub> _hubContext;
        private readonly IUserService _userService;
        
        public VoteController(IPollService polls, IHubContext<ResultsHub> hubContext, IUserService userService) 
        { 
            _polls = polls;
            _hubContext = hubContext;
            _userService = userService;
        }

        [HttpGet]
        [Route("Client/Vote/{pollId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(int pollId)
        {
            var poll = await _polls.GetPollAsync(pollId);
            if (poll is null) return NotFound();

            // Check if user is authenticated
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                // For non-authenticated users, show poll details but not voting functionality
                ViewBag.HasVoted = false;
                ViewBag.VoteCount = 0;
                ViewBag.MaxVotes = poll.MaxVotesPerVoter;
                ViewBag.UserVotes = new List<int>();
                ViewBag.ShowResults = false;
                ViewBag.PollStatus = _polls.GetPollStatus(poll);
                ViewBag.IsAuthenticated = false;
                ViewBag.AllowUsercodeEntry = poll.AllowUsercodeEntry;

                return View(poll);
            }

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

            // Attendance check
            bool isPresent = true; // default: no restriction
            if (poll.RequireAttendance)
                isPresent = await _polls.IsUserPresentAsync(pollId, userId);

            ViewBag.HasVoted = hasVoted;
            ViewBag.VoteCount = voteCount;
            ViewBag.MaxVotes = poll.MaxVotesPerVoter;
            ViewBag.UserVotes = userVotes;
            ViewBag.ShowResults = showResults;
            ViewBag.PollStatus = pollStatus;
            ViewBag.IsAuthenticated = true;
            ViewBag.AllowUsercodeEntry = poll.AllowUsercodeEntry;
            ViewBag.RequireAttendance = poll.RequireAttendance;
            ViewBag.IsPresent = isPresent;

            // Pass weighted total when results are visible
            if (showResults)
            {
                var weighted = await _polls.GetWeightedResultsAsync(pollId);
                ViewBag.WeightedTotal = weighted.Values.Sum();
            }

            return View(poll);
        }

        // Kiosk entry — sign in by voter code or username (no password) for polls that allow it
        [HttpPost]
        [Route("Client/Vote/KioskEntry")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KioskEntry(int pollId, string username)
        {
            var poll = await _polls.GetPollAsync(pollId);
            if (poll is null) return NotFound();

            // Guard: poll must have kiosk mode enabled
            if (!poll.AllowUsercodeEntry)
            {
                TempData["Error"] = "Unique code-only entry is not enabled for this poll.";
                return RedirectToAction(nameof(Index), new { pollId });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["KioskError"] = "Please enter your voter code or username.";
                return RedirectToAction(nameof(Index), new { pollId });
            }

            var trimmed = username.Trim();

            // Try voter code first (4 chars, case-insensitive), then fall back to username
            AppUser? user = null;
            if (trimmed.Length == 4)
                user = await _userService.GetByVoterCodeAsync(trimmed);

            if (user is null)
                user = await _userService.GetByUsernameAsync(trimmed);

            if (user is null || user.Role == "Admin")
            {
                TempData["KioskError"] = "Code not recognised. Please check and try again.";
                return RedirectToAction(nameof(Index), new { pollId });
            }

            if (user.IsTestAccount)
            {
                TempData["KioskError"] = "This is a test account and cannot participate in polls.";
                return RedirectToAction(nameof(Index), new { pollId });
            }

            // Sign in the voter without password — session cookie, short-lived
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("LoginTime", DateTime.UtcNow.ToString("o")),
                new Claim("KioskEntry", "true")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(4),
                    AllowRefresh = true
                });

            return RedirectToAction(nameof(Index), new { pollId });
        }

        [HttpPost]
        [Route("Client/Vote/Cast")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("voting")]
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
