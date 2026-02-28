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
    }
}
