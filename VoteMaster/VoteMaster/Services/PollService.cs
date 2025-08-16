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

        public async Task<Poll> CreatePollAsync(Poll poll, IEnumerable<string> options)
        {
            foreach (var text in options)
                poll.Options.Add(new PollOption { Text = text });

            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();
            return poll;
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
    }
}
