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
        // Owner-scoped versions for admin dashboard
        Task<List<Poll>> GetPollsForOwnerAsync(string status, int ownerId);
        Task<Poll> CreatePollAsync(Poll poll, IEnumerable<string> options);
        Task UpdatePollAsync(Poll poll);
        Task CastVoteAsync(int optionId, int userId);
        Task<Dictionary<string, int>> GetWeightedResultsAsync(int pollId);
        Task DeletePollAsync(int id);
        Task<List<VoterStatusDto>> GetVoterVotingStatusAsync(int pollId);
        Task<int> GetUserVoteCountAsync(int pollId, int userId);
        Task<bool> HasUserVotedAsync(int pollId, int userId);
        Task<List<int>> GetUserVotesForPollAsync(int pollId, int userId);
        Task ResetUserVotesAsync(int pollId, int userId);
        string GetPollStatus(Poll poll);
        // Proxy voting — admin votes on behalf of multiple users at once
        /// <summary>
        /// Casts votes for multiple users in bulk. Each entry maps a userId to the optionIds they voted for.
        /// Skips users who have already voted, and skips duplicate votes silently.
        /// Returns a summary: how many users were processed and how many were skipped.
        /// </summary>
        Task<ProxyVoteResult> ProxyCastVotesAsync(int pollId, Dictionary<int, List<int>> userOptionMap);

        // Poll lifecycle — reactivate an archived/ended poll
        Task ReactivatePollAsync(int pollId, DateTime newEndTime);

        // Admin vote reset
        Task ResetAllVotesAsync(int pollId);
        Task ResetSelectedVotesAsync(int pollId, IEnumerable<int> userIds);

        // Sharing
        Task SharePollAsync(int pollId, int withUserId);
        Task UnsharePollAsync(int pollId, int withUserId);
        Task<List<AppUser>> GetSharedUsersAsync(int pollId);
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

    public class ProxyVoteResult
    {
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public List<string> SkippedUsernames { get; set; } = new();
    }
}
