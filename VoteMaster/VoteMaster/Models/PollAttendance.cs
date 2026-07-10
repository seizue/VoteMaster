namespace VoteMaster.Models
{
    /// <summary>
    /// Records that a voter has been marked as present for a specific poll.
    /// Only created by an admin. When Poll.RequireAttendance is true,
    /// a voter must have a record here before they can cast a vote.
    /// </summary>
    public class PollAttendance
    {
        public int Id { get; set; }

        public int PollId { get; set; }
        public Poll Poll { get; set; } = null!;

        public int UserId { get; set; }
        public AppUser User { get; set; } = null!;

        /// <summary>UTC time the admin marked this voter as present.</summary>
        public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Which admin marked them present (null = system/import).</summary>
        public int? MarkedByAdminId { get; set; }
        public AppUser? MarkedByAdmin { get; set; }
    }
}
