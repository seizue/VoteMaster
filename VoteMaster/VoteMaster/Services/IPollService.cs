using VoteMaster.Models;

namespace VoteMaster.Services
{
    public interface IPollService
    {
        Task<Poll?> GetPollAsync(int id);
        Task<List<Poll>> GetActivePollsAsync();
        Task<List<Poll>> GetAllPollsAsync();
        Task<List<Poll>> GetArchivedPollsAsync();
        Task<List<Poll>> GetUpcomingPollsAsync();
        Task<List<Poll>> GetPollsAsync(string status);
        Task<Poll> CreatePollAsync(Poll poll, IEnumerable<string> options);
        Task CastVoteAsync(int optionId, int userId);
        Task<Dictionary<string, int>> GetWeightedResultsAsync(int pollId);
        Task DeletePollAsync(int id);
        Task<List<VoterStatusDto>> GetVoterVotingStatusAsync(int pollId);
        Task<int> GetUserVoteCountAsync(int pollId, int userId);
        Task<bool> HasUserVotedAsync(int pollId, int userId);
        string GetPollStatus(Poll poll);
    }

    public class VoterStatusDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool HasVoted { get; set; }
        public int VoteCount { get; set; }
    }

    public class PollViewDto
    {
        public Poll Poll { get; set; } = null!;
        public string Status { get; set; } = string.Empty;
    }
}
