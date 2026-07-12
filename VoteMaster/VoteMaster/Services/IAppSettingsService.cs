namespace VoteMaster.Services
{
    public interface IAppSettingsService
    {
        Task<string?> GetNetworkBaseUrlAsync();
        Task SetNetworkBaseUrlAsync(string? url);
    }
}
