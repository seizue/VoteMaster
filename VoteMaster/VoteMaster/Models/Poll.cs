using System.ComponentModel.DataAnnotations;

namespace VoteMaster.Models
{
    public class Poll
    {
        public int Id { get; set; }
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; } = DateTime.UtcNow.AddHours(2);
        public bool AllowPublicResults { get; set; } = true;
        public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
        public int MaxVotesPerVoter { get; set; } = 1;
        public int MinVotesPerVoter { get; set; } = 1;

        // Real-time features
        public bool EnableLiveVoteCount { get; set; } = false;
        public bool EnablePollNotifications { get; set; } = false;

        // Kiosk mode — voters can enter using only their username (no password required)
        public bool AllowUsercodeEntry { get; set; } = false;

        // Attendance mode — voters must be marked present before they can vote
        public bool RequireAttendance { get; set; } = false;
        public ICollection<PollAttendance> Attendances { get; set; } = new List<PollAttendance>();

        // Ownership — nullable so existing polls without an owner still work
        public int? OwnerId { get; set; }
        public AppUser? Owner { get; set; }

        // Shared admins
        public ICollection<PollShare> Shares { get; set; } = new List<PollShare>();
    }
}
