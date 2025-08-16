using Microsoft.AspNetCore.Mvc;
using VoteMaster.Services;

namespace VoteMaster.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _polls;
        public PollsController(IPollService polls) { _polls = polls; }

        [HttpGet]
        public async Task<IActionResult> GetActive() => Ok(await _polls.GetActivePollsAsync());

        [HttpGet("{id:int}/results")]
        public async Task<IActionResult> Results(int id) => Ok(await _polls.GetWeightedResultsAsync(id));
    }
}
