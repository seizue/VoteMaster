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
        Task<List<VoterStatusDto>> GetVoterVotingStatusAsync(int pollId);
        Task<int> GetUserVoteCountAsync(int pollId, int userId);
        Task<bool> HasUserVotedAsync(int pollId, int userId);
    }

    public class VoterStatusDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool HasVoted { get; set; }
        public int VoteCount { get; set; }
    }
}
