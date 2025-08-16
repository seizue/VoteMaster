using VoteMaster.Models;

namespace VoteMaster.Services
{
    public interface IPollService
    {
        Task<Poll?> GetPollAsync(int id);
        Task<List<Poll>> GetActivePollsAsync();
        Task<Poll> CreatePollAsync(Poll poll, IEnumerable<string> options);
        Task CastVoteAsync(int optionId, int userId);
        Task<Dictionary<string, int>> GetWeightedResultsAsync(int pollId);
        Task DeletePollAsync(int id);
    }
}
