using Microsoft.EntityFrameworkCore;
using VoteMaster.Data;
using VoteMaster.Models;

namespace VoteMaster.Services
{
    public class PollService : IPollService
    {
        private readonly AppDbContext _db;
        public PollService(AppDbContext db) { _db = db; }

        public async Task<Poll?> GetPollAsync(int id) =>
            await _db.Polls.Include(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<List<Poll>> GetActivePollsAsync()
        {
            var now = DateTime.UtcNow;
            return await _db.Polls.Include(p => p.Options)
                .Where(p => p.StartTime <= now && p.EndTime >= now)
                .ToListAsync();
        }

        public async Task<List<Poll>> GetAllPollsAsync()
        {
            return await _db.Polls.Include(p => p.Options)
                .OrderByDescending(p => p.StartTime)
                .ToListAsync();
        }

        public async Task<List<Poll>> GetArchivedPollsAsync()
        {
            var now = DateTime.UtcNow;
            return await _db.Polls.Include(p => p.Options)
                .Where(p => p.EndTime < now)
                .OrderByDescending(p => p.EndTime)
                .ToListAsync();
        }

        public async Task<List<Poll>> GetUpcomingPollsAsync()
        {
            var now = DateTime.UtcNow;
            return await _db.Polls.Include(p => p.Options)
                .Where(p => p.StartTime > now)
                .OrderBy(p => p.StartTime)
                .ToListAsync();
        }

        public async Task<List<Poll>> GetPollsAsync(string status)
        {
            return status.ToLower() switch
            {
                "active" => await GetActivePollsAsync(),
                "archived" => await GetArchivedPollsAsync(),
                "upcoming" => await GetUpcomingPollsAsync(),
                _ => await GetAllPollsAsync()
            };
        }

        public string GetPollStatus(Poll poll)
        {
            var now = DateTime.UtcNow;
            if (poll.StartTime > now)
                return "Upcoming";
            if (poll.EndTime < now)
                return "Archived";
            return "Active";
        }

        public async Task<Poll> CreatePollAsync(Poll poll, IEnumerable<string> options)
        {
            foreach (var text in options)
                poll.Options.Add(new PollOption { Text = text });

            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();
            return poll;
        }

        public async Task UpdatePollAsync(Poll poll)
        {
            _db.Polls.Update(poll);
            await _db.SaveChangesAsync();
        }

        public async Task CastVoteAsync(int optionId, int userId)
        {
            var option = await _db.Options
                .Include(o => o.Poll)
                .FirstOrDefaultAsync(o => o.Id == optionId)
                ?? throw new InvalidOperationException("Option not found");

            var pollId = option.PollId;

            // Count how many votes the user has already cast in this poll
            var existingVotes = await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.UserId == userId && v.Option.PollId == pollId)
                .CountAsync();

            // Check if user has reached the maximum votes allowed
            if (existingVotes >= option.Poll.MaxVotesPerVoter)
            {
                throw new InvalidOperationException($"Maximum of {option.Poll.MaxVotesPerVoter} votes allowed per poll");
            }

            // Check if user has already voted for this specific option
            var alreadyVotedForOption = await _db.Votes
                .AnyAsync(v => v.UserId == userId && v.OptionId == optionId);

            if (alreadyVotedForOption)
            {
                throw new InvalidOperationException("You have already voted for this option");
            }

            _db.Votes.Add(new Vote
            {
                OptionId = optionId,
                UserId = userId,
                VotedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        public async Task<Dictionary<string, int>> GetWeightedResultsAsync(int pollId)
        {
            var poll = await _db.Polls
                .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
                .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll is null) return new();

            return poll.Options.ToDictionary(
                o => o.Text,
                o => o.Votes.Sum(v => v.User.Weight));
        }
        
 public async Task DeletePollAsync(int id)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll != null)
            {
                _db.Polls.Remove(poll);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<VoterStatusDto>> GetVoterVotingStatusAsync(int pollId)
        {
            // Get all voters
            var allVoters = await _db.Users
                .Where(u => u.Role == "Voter")
                .ToListAsync();

            // Get votes for this poll
            var pollVotes = await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.Option.PollId == pollId)
                .GroupBy(v => v.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // Create DTO with voting status
            return allVoters.Select(voter => new VoterStatusDto
            {
                UserId = voter.Id,
                Username = voter.Username,
                HasVoted = pollVotes.ContainsKey(voter.Id),
                VoteCount = pollVotes.ContainsKey(voter.Id) ? pollVotes[voter.Id] : 0
            }).ToList();
        }

        public async Task<int> GetUserVoteCountAsync(int pollId, int userId)
        {
            return await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.UserId == userId && v.Option.PollId == pollId)
                .CountAsync();
        }

        public async Task<bool> HasUserVotedAsync(int pollId, int userId)
        {
            return await _db.Votes
                .Include(v => v.Option)
                .AnyAsync(v => v.UserId == userId && v.Option.PollId == pollId);
        }
    }
}
