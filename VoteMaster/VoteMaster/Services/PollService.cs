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

        // Returns polls owned by OR shared with the given admin
        public async Task<List<Poll>> GetPollsForOwnerAsync(string status, int ownerId)
        {
            var now = DateTime.UtcNow;
            var query = _db.Polls.Include(p => p.Options)
                .Where(p => p.OwnerId == ownerId
                         || p.Shares.Any(s => s.SharedWithUserId == ownerId));

            query = status.ToLower() switch
            {
                "active"   => query.Where(p => p.StartTime <= now && p.EndTime >= now),
                "archived" => query.Where(p => p.EndTime < now),
                "upcoming" => query.Where(p => p.StartTime > now),
                _          => query
            };

            return await query.OrderByDescending(p => p.StartTime).ToListAsync();
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

            // Attendance guard — if the poll requires it, the voter must be marked present
            if (option.Poll.RequireAttendance)
            {
                var isPresent = await _db.PollAttendances
                    .AnyAsync(a => a.PollId == pollId && a.UserId == userId);
                if (!isPresent)
                    throw new InvalidOperationException("You are not marked as present for this poll. Please contact the administrator.");
            }

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

            // Get attendance for this poll
            var presentIds = await _db.PollAttendances
                .Where(a => a.PollId == pollId)
                .Select(a => a.UserId)
                .ToHashSetAsync();

            return allVoters.Select(voter => new VoterStatusDto
            {
                UserId    = voter.Id,
                Username  = voter.Username,
                HasVoted  = pollVotes.ContainsKey(voter.Id),
                VoteCount = pollVotes.ContainsKey(voter.Id) ? pollVotes[voter.Id] : 0,
                IsPresent = presentIds.Contains(voter.Id)
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

        public async Task<List<int>> GetUserVotesForPollAsync(int pollId, int userId)
        {
            return await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.UserId == userId && v.Option.PollId == pollId)
                .Select(v => v.OptionId)
                .ToListAsync();
        }

        public async Task ResetUserVotesAsync(int pollId, int userId)
        {
            var votesToRemove = await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.UserId == userId && v.Option.PollId == pollId)
                .ToListAsync();

            if (votesToRemove.Any())
            {
                _db.Votes.RemoveRange(votesToRemove);
                await _db.SaveChangesAsync();
            }
        }

        public async Task SharePollAsync(int pollId, int withUserId)
        {
            var exists = await _db.PollShares
                .AnyAsync(s => s.PollId == pollId && s.SharedWithUserId == withUserId);
            if (!exists)
            {
                _db.PollShares.Add(new PollShare { PollId = pollId, SharedWithUserId = withUserId });
                await _db.SaveChangesAsync();
            }
        }

        public async Task UnsharePollAsync(int pollId, int withUserId)
        {
            var share = await _db.PollShares
                .FirstOrDefaultAsync(s => s.PollId == pollId && s.SharedWithUserId == withUserId);
            if (share != null)
            {
                _db.PollShares.Remove(share);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<AppUser>> GetSharedUsersAsync(int pollId) =>
            await _db.PollShares
                .Where(s => s.PollId == pollId)
                .Select(s => s.SharedWithUser)
                .ToListAsync();

        public async Task ReactivatePollAsync(int pollId, DateTime newEndTime)
        {
            var poll = await _db.Polls.FindAsync(pollId)
                ?? throw new InvalidOperationException("Poll not found");

            var now = DateTime.UtcNow;
            // If start time is also in the past, reset it to now so the poll is immediately active
            if (poll.StartTime > now) poll.StartTime = now;
            poll.EndTime = newEndTime.ToUniversalTime();

            _db.Polls.Update(poll);
            await _db.SaveChangesAsync();
        }

        public async Task ResetAllVotesAsync(int pollId)
        {
            var votes = await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.Option.PollId == pollId)
                .ToListAsync();

            if (votes.Any())
            {
                _db.Votes.RemoveRange(votes);
                await _db.SaveChangesAsync();
            }
        }

        public async Task ResetSelectedVotesAsync(int pollId, IEnumerable<int> userIds)
        {
            var idSet = userIds.ToHashSet();
            var votes = await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.Option.PollId == pollId && idSet.Contains(v.UserId))
                .ToListAsync();

            if (votes.Any())
            {
                _db.Votes.RemoveRange(votes);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<ProxyVoteResult> ProxyCastVotesAsync(int pollId, Dictionary<int, List<int>> userOptionMap)
        {
            var poll = await _db.Polls
                .Include(p => p.Options)
                .FirstOrDefaultAsync(p => p.Id == pollId)
                ?? throw new InvalidOperationException("Poll not found");

            // Pre-load all existing votes for this poll in one query
            var existingVotes = await _db.Votes
                .Include(v => v.Option)
                .Where(v => v.Option.PollId == pollId)
                .Select(v => new { v.UserId, v.OptionId })
                .ToListAsync();

            var votedUserIds = existingVotes.Select(v => v.UserId).ToHashSet();
            var existingPairs = existingVotes.Select(v => (v.UserId, v.OptionId)).ToHashSet();

            // Load usernames for the skipped-users report
            var userIds = userOptionMap.Keys.ToList();
            var users = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            // If attendance required, pre-load who is already present
            HashSet<int>? presentIds = null;
            if (poll.RequireAttendance)
            {
                presentIds = await _db.PollAttendances
                    .Where(a => a.PollId == pollId)
                    .Select(a => a.UserId)
                    .ToHashSetAsync();
            }

            var result = new ProxyVoteResult();
            var newVotes = new List<Vote>();
            var newAttendances = new List<PollAttendance>();

            foreach (var (userId, optionIds) in userOptionMap)
            {
                // Skip users who already voted
                if (votedUserIds.Contains(userId))
                {
                    result.Skipped++;
                    if (users.TryGetValue(userId, out var uname))
                        result.SkippedUsernames.Add(uname);
                    continue;
                }

                // Validate option count against poll constraints
                var validOptions = optionIds
                    .Distinct()
                    .Where(oid => poll.Options.Any(o => o.Id == oid))
                    .ToList();

                if (validOptions.Count < poll.MinVotesPerVoter || validOptions.Count > poll.MaxVotesPerVoter)
                {
                    result.Skipped++;
                    if (users.TryGetValue(userId, out var uname))
                        result.SkippedUsernames.Add(uname);
                    continue;
                }

                // Auto-mark present when admin casts proxy vote
                if (poll.RequireAttendance && presentIds != null && !presentIds.Contains(userId))
                {
                    newAttendances.Add(new PollAttendance
                    {
                        PollId   = pollId,
                        UserId   = userId,
                        MarkedAt = DateTime.UtcNow
                    });
                    presentIds.Add(userId);
                }

                foreach (var optionId in validOptions)
                {
                    if (!existingPairs.Contains((userId, optionId)))
                    {
                        newVotes.Add(new Vote
                        {
                            OptionId = optionId,
                            UserId   = userId,
                            VotedAt  = DateTime.UtcNow
                        });
                        existingPairs.Add((userId, optionId));
                    }
                }

                result.Processed++;
            }

            if (newAttendances.Count > 0)
                _db.PollAttendances.AddRange(newAttendances);

            if (newVotes.Count > 0)
                _db.Votes.AddRange(newVotes);

            if (newAttendances.Count > 0 || newVotes.Count > 0)
                await _db.SaveChangesAsync();

            return result;
        }

        // ── Attendance ────────────────────────────────────────────────────────────

        public async Task<HashSet<int>> GetPresentUserIdsAsync(int pollId) =>
            (await _db.PollAttendances
                .Where(a => a.PollId == pollId)
                .Select(a => a.UserId)
                .ToListAsync())
            .ToHashSet();

        public async Task<bool> IsUserPresentAsync(int pollId, int userId) =>
            await _db.PollAttendances.AnyAsync(a => a.PollId == pollId && a.UserId == userId);

        public async Task<int> GetPresentWeightTotalAsync(int pollId) =>
            await _db.PollAttendances
                .Where(a => a.PollId == pollId)
                .Join(_db.Users, a => a.UserId, u => u.Id, (a, u) => u.Weight)
                .SumAsync();

        public async Task MarkPresentAsync(int pollId, IEnumerable<int> userIds, int markedByAdminId)
        {
            var idList  = userIds.Distinct().ToList();
            var already = await _db.PollAttendances
                .Where(a => a.PollId == pollId && idList.Contains(a.UserId))
                .Select(a => a.UserId)
                .ToHashSetAsync();

            var toAdd = idList
                .Where(uid => !already.Contains(uid))
                .Select(uid => new PollAttendance
                {
                    PollId         = pollId,
                    UserId         = uid,
                    MarkedAt       = DateTime.UtcNow,
                    MarkedByAdminId = markedByAdminId
                });

            _db.PollAttendances.AddRange(toAdd);
            await _db.SaveChangesAsync();
        }

        public async Task MarkAbsentAsync(int pollId, IEnumerable<int> userIds)
        {
            var idSet = userIds.ToHashSet();
            var rows  = await _db.PollAttendances
                .Where(a => a.PollId == pollId && idSet.Contains(a.UserId))
                .ToListAsync();

            if (rows.Any())
            {
                _db.PollAttendances.RemoveRange(rows);
                await _db.SaveChangesAsync();
            }
        }
    }
}
