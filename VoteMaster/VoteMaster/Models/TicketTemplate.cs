using System.ComponentModel.DataAnnotations;

namespace VoteMaster.Models
{
    public class TicketTemplate
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // Path to uploaded header image (relative to wwwroot)
        public string? HeaderImagePath { get; set; }

        // The poll this ticket is linked to
        public int PollId { get; set; }
        public Poll? Poll { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
