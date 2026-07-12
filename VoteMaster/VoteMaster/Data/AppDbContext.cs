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
        public DbSet<PollAttendance> PollAttendances => Set<PollAttendance>();
        public DbSet<AppSettings> AppSettings => Set<AppSettings>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>(e =>
            {
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.VoterCode).IsUnique().HasFilter("[VoterCode] IS NOT NULL");
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

            // PollAttendance — one record per voter per poll
            modelBuilder.Entity<PollAttendance>(e =>
            {
                e.HasIndex(a => new { a.PollId, a.UserId }).IsUnique();
                e.HasOne(a => a.Poll)
                 .WithMany(p => p.Attendances)
                 .HasForeignKey(a => a.PollId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.User)
                 .WithMany()
                 .HasForeignKey(a => a.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.MarkedByAdmin)
                 .WithMany()
                 .HasForeignKey(a => a.MarkedByAdminId)
                 .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
