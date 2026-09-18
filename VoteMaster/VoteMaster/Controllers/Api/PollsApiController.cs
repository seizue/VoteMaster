using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VoteMaster.Services;

namespace VoteMaster.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [EnableRateLimiting("api")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _polls;
        public PollsController(IPollService polls) { _polls = polls; }

        [HttpGet]
        public async Task<IActionResult> GetActive() => Ok(await _polls.GetActivePollsAsync());

        [HttpGet("{id:int}/results")]
        public async Task<IActionResult> Results(int id)
        {
            var poll = await _polls.GetPollAsync(id);
            if (poll is null) return NotFound();

            // Respect AllowPublicResults — deny access if results are private
            if (!poll.AllowPublicResults)
                return StatusCode(403, new { error = "Results for this poll are not publicly available." });

            return Ok(await _polls.GetWeightedResultsAsync(id));
        }
    }
}
