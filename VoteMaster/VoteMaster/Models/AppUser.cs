namespace VoteMaster.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // Plain-text password stored so admins can view/reset voter credentials.
        // Only populated for admin-created accounts; null for self-registered (Google) users.
        public string? PlainPassword { get; set; }

        // Full name of the shareholder / voter
        public string? FullName { get; set; }

        // 4-character unique voter code — alternative identifier for kiosk / ticket-based voting
        public string? VoterCode { get; set; }

        // Test/demo account — excluded from voting and weight calculations
        public bool IsTestAccount { get; set; } = false;

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
