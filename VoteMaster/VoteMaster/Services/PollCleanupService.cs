using Microsoft.EntityFrameworkCore;
using VoteMaster.Data;

namespace VoteMaster.Services
{
    public class PollCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PollCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public PollCleanupService(IServiceScopeFactory scopeFactory, ILogger<PollCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Poll cleanup service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupExpiredPollsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task CleanupExpiredPollsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-7);

                var expiredPolls = await db.Polls
                    .Where(p => p.EndTime < cutoff)
                    .ToListAsync(cancellationToken);

                if (expiredPolls.Count == 0)
                {
                    _logger.LogInformation("No expired polls to clean up.");
                    return;
                }

                // EF cascade delete will handle Options and Votes
                db.Polls.RemoveRange(expiredPolls);
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Deleted {Count} expired poll(s) that ended before {Cutoff}.",
                    expiredPolls.Count, cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during poll cleanup.");
            }
        }
    }
}
