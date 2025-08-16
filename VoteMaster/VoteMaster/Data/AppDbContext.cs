using Microsoft.EntityFrameworkCore;
using VoteMaster.Models;

namespace VoteMaster.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<PollOption> Options => Set<PollOption>();
        public DbSet<Vote> Votes => Set<Vote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>(e =>
            {
                e.HasIndex(u => u.Username).IsUnique();
            });

            modelBuilder.Entity<Vote>(e =>
            {
                e.HasIndex(v => new { v.UserId, v.OptionId }).IsUnique();
            });
        }
    }
}
