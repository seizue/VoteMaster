namespace VoteMaster.Models
{
    public class Vote
    {
        public int Id { get; set; }
        public int OptionId { get; set; }
        public PollOption Option { get; set; } = null!;
        public int UserId { get; set; }
        public AppUser User { get; set; } = null!;
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    }
}
