namespace VoteMaster.Models.Analytics
{
    public class PollAnalyticsDto
    {
        public int PollId { get; set; }
        public string PollTitle { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalVoters { get; set; }
        public int VotedCount { get; set; }
        public double ParticipationRate { get; set; }
        public int TotalVotesCast { get; set; }
        public int TotalWeightedVotes { get; set; }
        public List<OptionAnalyticsDto> OptionAnalytics { get; set; } = new();
        public List<VotingTimeSeriesDto> VotingTimeSeries { get; set; } = new();
        public List<DemographicBreakdownDto> DemographicBreakdown { get; set; } = new();
    }

    public class OptionAnalyticsDto
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public int VoteCount { get; set; }
        public int WeightedVoteCount { get; set; }
        public double Percentage { get; set; }
        public double WeightedPercentage { get; set; }
    }

    public class VotingTimeSeriesDto
    {
        public DateTime Timestamp { get; set; }
        public int CumulativeVotes { get; set; }
        public int CumulativeVoters { get; set; }
        public string OptionText { get; set; } = string.Empty;
    }

    public class DemographicBreakdownDto
    {
        public string Role { get; set; } = string.Empty;
        public int VoterCount { get; set; }
        public int VotedCount { get; set; }
        public double ParticipationRate { get; set; }
        public int TotalWeight { get; set; }
        public int WeightUsed { get; set; }
    }

    public class HistoricalTrendDto
    {
        public DateTime Date { get; set; }
        public int PollsCreated { get; set; }
        public int TotalVotes { get; set; }
        public double AverageParticipation { get; set; }
    }
}
