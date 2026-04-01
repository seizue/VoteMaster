namespace VoteMaster.Models
{
    public class PollShare
    {
        public int Id { get; set; }
        public int PollId { get; set; }
        public Poll Poll { get; set; } = null!;
        public int SharedWithUserId { get; set; }
        public AppUser SharedWithUser { get; set; } = null!;
        public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    }
}
