namespace VoteMaster.Models
{
    public class PollOption
    {
        public int Id { get; set; }
        public int PollId { get; set; }
        public Poll Poll { get; set; } = null!;
        public string Text { get; set; } = string.Empty;
        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
