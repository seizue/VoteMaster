using VoteMaster.Models.Analytics;

namespace VoteMaster.Services
{
    public interface IAnalyticsService
    {
        Task<PollAnalyticsDto> GetPollAnalyticsAsync(int pollId);
        Task<List<HistoricalTrendDto>> GetHistoricalTrendsAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> ExportPollResultsToCsvAsync(int pollId);
        Task<byte[]> ExportPollResultsToPdfAsync(int pollId);
        Task<List<PollAnalyticsDto>> GetAllPollsAnalyticsAsync();
    }
}
