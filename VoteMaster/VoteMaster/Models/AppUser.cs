namespace VoteMaster.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Voter"; // "Admin" or "Voter"
        public int Weight { get; set; } = 1;

        // Optional email — used to link Google OAuth sign-ins to admin-created accounts
        public string? Email { get; set; }

        public ICollection<Vote> Votes { get; set; } = new List<Vote>();

        // Which admin created this user (null = seeded/system user, visible to all admins)
        public int? CreatedByAdminId { get; set; }
        public AppUser? CreatedByAdmin { get; set; }
    }
}
