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
        public DbSet<TicketTemplate> TicketTemplates => Set<TicketTemplate>();
        public DbSet<PollShare> PollShares => Set<PollShare>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>(e =>
            {
                e.HasIndex(u => u.Username).IsUnique();
                e.HasOne(u => u.CreatedByAdmin)
                 .WithMany()
                 .HasForeignKey(u => u.CreatedByAdminId)
                 .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Vote>(e =>
            {
                e.HasIndex(v => new { v.UserId, v.OptionId }).IsUnique();
            });

            // Poll ownership — no cascade delete on owner to avoid accidental poll loss
            modelBuilder.Entity<Poll>(e =>
            {
                e.HasOne(p => p.Owner)
                 .WithMany()
                 .HasForeignKey(p => p.OwnerId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // PollShare — unique per poll+user pair
            modelBuilder.Entity<PollShare>(e =>
            {
                e.HasIndex(s => new { s.PollId, s.SharedWithUserId }).IsUnique();
                e.HasOne(s => s.Poll)
                 .WithMany(p => p.Shares)
                 .HasForeignKey(s => s.PollId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(s => s.SharedWithUser)
                 .WithMany()
                 .HasForeignKey(s => s.SharedWithUserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
