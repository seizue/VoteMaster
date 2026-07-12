namespace VoteMaster.Models
{
    /// <summary>
    /// Single-row application-wide settings stored in the database.
    /// </summary>
    public class AppSettings
    {
        public int Id { get; set; }

        /// <summary>
        /// LAN base URL set by the admin in the ticket template page,
        /// e.g. "http://192.168.1.100:5000". Null = use server host.
        /// </summary>
        public string? NetworkBaseUrl { get; set; }
    }
}
