using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using CsvHelper;
using iTextSharp.text;
using iTextSharp.text.pdf;
using VoteMaster.Data;
using VoteMaster.Models.Analytics;

namespace VoteMaster.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _db;

        public AnalyticsService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PollAnalyticsDto> GetPollAnalyticsAsync(int pollId)
        {
            var poll = await _db.Polls
                .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
                .ThenInclude(v => v.User)
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll == null)
                throw new InvalidOperationException("Poll not found");

            var allVoters = await _db.Users.Where(u => u.Role == "Voter").ToListAsync();
            var votedUserIds = poll.Options
                .SelectMany(o => o.Votes)
                .Select(v => v.UserId)
                .Distinct()
                .ToList();

            var totalVotes = poll.Options.Sum(o => o.Votes.Count);
            var totalWeightedVotes = poll.Options.Sum(o => o.Votes.Sum(v => v.User.Weight));

            var analytics = new PollAnalyticsDto
            {
                PollId = poll.Id,
                PollTitle = poll.Title,
                StartTime = poll.StartTime,
                EndTime = poll.EndTime,
                TotalVoters = allVoters.Count,
                VotedCount = votedUserIds.Count,
                ParticipationRate = allVoters.Count > 0 ? (double)votedUserIds.Count / allVoters.Count * 100 : 0,
                TotalVotesCast = totalVotes,
                TotalWeightedVotes = totalWeightedVotes
            };

            // Option analytics
            foreach (var option in poll.Options)
            {
                var voteCount = option.Votes.Count;
                var weightedVoteCount = option.Votes.Sum(v => v.User.Weight);

                analytics.OptionAnalytics.Add(new OptionAnalyticsDto
                {
                    OptionId = option.Id,
                    OptionText = option.Text,
                    VoteCount = voteCount,
                    WeightedVoteCount = weightedVoteCount,
                    Percentage = totalVotes > 0 ? (double)voteCount / totalVotes * 100 : 0,
                    WeightedPercentage = totalWeightedVotes > 0 ? (double)weightedVoteCount / totalWeightedVotes * 100 : 0
                });
            }

            // Time series data
            var allVotes = poll.Options
                .SelectMany(o => o.Votes.Select(v => new { v.VotedAt, o.Text, v.UserId }))
                .OrderBy(v => v.VotedAt)
                .ToList();

            var cumulativeVotes = 0;
            var seenVoters = new HashSet<int>();

            foreach (var vote in allVotes)
            {
                cumulativeVotes++;
                seenVoters.Add(vote.UserId);

                analytics.VotingTimeSeries.Add(new VotingTimeSeriesDto
                {
                    Timestamp = vote.VotedAt,
                    CumulativeVotes = cumulativeVotes,
                    CumulativeVoters = seenVoters.Count,
                    OptionText = vote.Text
                });
            }

            // Demographic breakdown by role
            var roleGroups = allVoters.GroupBy(u => u.Role);
            foreach (var roleGroup in roleGroups)
            {
                var roleVoters = roleGroup.ToList();
                var roleVotedCount = roleVoters.Count(v => votedUserIds.Contains(v.Id));
                var totalWeight = roleVoters.Sum(v => v.Weight);
                var weightUsed = poll.Options
                    .SelectMany(o => o.Votes)
                    .Where(v => roleVoters.Select(rv => rv.Id).Contains(v.UserId))
                    .Sum(v => v.User.Weight);

                analytics.DemographicBreakdown.Add(new DemographicBreakdownDto
                {
                    Role = roleGroup.Key,
                    VoterCount = roleVoters.Count,
                    VotedCount = roleVotedCount,
                    ParticipationRate = roleVoters.Count > 0 ? (double)roleVotedCount / roleVoters.Count * 100 : 0,
                    TotalWeight = totalWeight,
                    WeightUsed = weightUsed
                });
            }

            return analytics;
        }

        public async Task<List<HistoricalTrendDto>> GetHistoricalTrendsAsync(DateTime startDate, DateTime endDate)
        {
            var polls = await _db.Polls
                .Include(p => p.Options)
                .ThenInclude(o => o.Votes)
                .Where(p => p.StartTime >= startDate && p.StartTime <= endDate)
                .ToListAsync();

            var allVoters = await _db.Users.Where(u => u.Role == "Voter").CountAsync();

            var trends = polls
                .GroupBy(p => p.StartTime.Date)
                .Select(g => new HistoricalTrendDto
                {
                    Date = g.Key,
                    PollsCreated = g.Count(),
                    TotalVotes = g.Sum(p => p.Options.Sum(o => o.Votes.Count)),
                    AverageParticipation = g.Average(p =>
                    {
                        var votedCount = p.Options.SelectMany(o => o.Votes).Select(v => v.UserId).Distinct().Count();
                        return allVoters > 0 ? (double)votedCount / allVoters * 100 : 0;
                    })
                })
                .OrderBy(t => t.Date)
                .ToList();

            return trends;
        }

        public async Task<byte[]> ExportPollResultsToCsvAsync(int pollId)
        {
            var analytics = await GetPollAnalyticsAsync(pollId);

            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write poll summary
            csv.WriteField("Poll Title");
            csv.WriteField(analytics.PollTitle);
            csv.NextRecord();

            csv.WriteField("Start Time");
            csv.WriteField(analytics.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            csv.NextRecord();

            csv.WriteField("End Time");
            csv.WriteField(analytics.EndTime.ToString("yyyy-MM-dd HH:mm:ss"));
            csv.NextRecord();

            csv.WriteField("Total Voters");
            csv.WriteField(analytics.TotalVoters);
            csv.NextRecord();

            csv.WriteField("Voted Count");
            csv.WriteField(analytics.VotedCount);
            csv.NextRecord();

            csv.WriteField("Participation Rate");
            csv.WriteField($"{analytics.ParticipationRate:F2}%");
            csv.NextRecord();

            csv.WriteField("Total Votes Cast");
            csv.WriteField(analytics.TotalVotesCast);
            csv.NextRecord();

            csv.WriteField("Total Weighted Votes");
            csv.WriteField(analytics.TotalWeightedVotes);
            csv.NextRecord();
            csv.NextRecord();

            // Write option results
            csv.WriteField("Option");
            csv.WriteField("Vote Count");
            csv.WriteField("Weighted Vote Count");
            csv.WriteField("Percentage");
            csv.WriteField("Weighted Percentage");
            csv.NextRecord();

            foreach (var option in analytics.OptionAnalytics)
            {
                csv.WriteField(option.OptionText);
                csv.WriteField(option.VoteCount);
                csv.WriteField(option.WeightedVoteCount);
                csv.WriteField($"{option.Percentage:F2}%");
                csv.WriteField($"{option.WeightedPercentage:F2}%");
                csv.NextRecord();
            }

            csv.NextRecord();

            // Write demographic breakdown
            if (analytics.DemographicBreakdown.Any())
            {
                csv.WriteField("Demographic Breakdown");
                csv.NextRecord();
                csv.WriteField("Role");
                csv.WriteField("Voter Count");
                csv.WriteField("Voted Count");
                csv.WriteField("Participation Rate");
                csv.WriteField("Total Weight");
                csv.WriteField("Weight Used");
                csv.NextRecord();

                foreach (var demo in analytics.DemographicBreakdown)
                {
                    csv.WriteField(demo.Role);
                    csv.WriteField(demo.VoterCount);
                    csv.WriteField(demo.VotedCount);
                    csv.WriteField($"{demo.ParticipationRate:F2}%");
                    csv.WriteField(demo.TotalWeight);
                    csv.WriteField(demo.WeightUsed);
                    csv.NextRecord();
                }
            }

            await writer.FlushAsync();
            return memoryStream.ToArray();
        }

        public async Task<byte[]> ExportPollResultsToPdfAsync(int pollId)
        {
            var analytics = await GetPollAnalyticsAsync(pollId);

            using var memoryStream = new MemoryStream();
            var document = new Document(PageSize.A4, 50, 50, 25, 25);
            var writer = PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // Title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
            var title = new Paragraph(analytics.PollTitle, titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // Poll Information
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

            document.Add(new Paragraph($"Start Time: {analytics.StartTime:yyyy-MM-dd HH:mm:ss}", normalFont));
            document.Add(new Paragraph($"End Time: {analytics.EndTime:yyyy-MM-dd HH:mm:ss}", normalFont));
            document.Add(new Paragraph($"Total Voters: {analytics.TotalVoters}", normalFont));
            document.Add(new Paragraph($"Voted Count: {analytics.VotedCount}", normalFont));
            document.Add(new Paragraph($"Participation Rate: {analytics.ParticipationRate:F2}%", normalFont));
            document.Add(new Paragraph($"Total Votes Cast: {analytics.TotalVotesCast}", normalFont));
            document.Add(new Paragraph($"Total Weighted Votes: {analytics.TotalWeightedVotes}", normalFont));
            document.Add(new Paragraph(" "));

            // Results Table
            var resultsTable = new PdfPTable(5)
            {
                WidthPercentage = 100,
                SpacingBefore = 10,
                SpacingAfter = 10
            };
            resultsTable.SetWidths(new float[] { 3, 1.5f, 1.5f, 1.5f, 1.5f });

            // Table headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
            var headerCells = new[]
            {
                "Option", "Votes", "Weighted", "Percentage", "Weighted %"
            };

            foreach (var header in headerCells)
            {
                var cell = new PdfPCell(new Phrase(header, headerFont))
                {
                    BackgroundColor = new BaseColor(52, 73, 94),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 5
                };
                resultsTable.AddCell(cell);
            }

            // Table data
            foreach (var option in analytics.OptionAnalytics)
            {
                resultsTable.AddCell(new PdfPCell(new Phrase(option.OptionText, normalFont)) { Padding = 5 });
                resultsTable.AddCell(new PdfPCell(new Phrase(option.VoteCount.ToString(), normalFont)) 
                { 
                    HorizontalAlignment = Element.ALIGN_CENTER, 
                    Padding = 5 
                });
                resultsTable.AddCell(new PdfPCell(new Phrase(option.WeightedVoteCount.ToString(), normalFont)) 
                { 
                    HorizontalAlignment = Element.ALIGN_CENTER, 
                    Padding = 5 
                });
                resultsTable.AddCell(new PdfPCell(new Phrase($"{option.Percentage:F2}%", normalFont)) 
                { 
                    HorizontalAlignment = Element.ALIGN_CENTER, 
                    Padding = 5 
                });
                resultsTable.AddCell(new PdfPCell(new Phrase($"{option.WeightedPercentage:F2}%", normalFont)) 
                { 
                    HorizontalAlignment = Element.ALIGN_CENTER, 
                    Padding = 5 
                });
            }

            document.Add(resultsTable);

            // Demographic Breakdown
            if (analytics.DemographicBreakdown.Any())
            {
                document.Add(new Paragraph("Demographic Breakdown", boldFont) { SpacingBefore = 20, SpacingAfter = 10 });

                var demoTable = new PdfPTable(6) { WidthPercentage = 100 };
                demoTable.SetWidths(new float[] { 2, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f });

                var demoHeaders = new[] { "Role", "Voters", "Voted", "Rate %", "Weight", "Used" };
                foreach (var header in demoHeaders)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = new BaseColor(52, 73, 94),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    demoTable.AddCell(cell);
                }

                foreach (var demo in analytics.DemographicBreakdown)
                {
                    demoTable.AddCell(new PdfPCell(new Phrase(demo.Role, normalFont)) { Padding = 5 });
                    demoTable.AddCell(new PdfPCell(new Phrase(demo.VoterCount.ToString(), normalFont)) 
                    { 
                        HorizontalAlignment = Element.ALIGN_CENTER, 
                        Padding = 5 
                    });
                    demoTable.AddCell(new PdfPCell(new Phrase(demo.VotedCount.ToString(), normalFont)) 
                    { 
                        HorizontalAlignment = Element.ALIGN_CENTER, 
                        Padding = 5 
                    });
                    demoTable.AddCell(new PdfPCell(new Phrase($"{demo.ParticipationRate:F2}%", normalFont)) 
                    { 
                        HorizontalAlignment = Element.ALIGN_CENTER, 
                        Padding = 5 
                    });
                    demoTable.AddCell(new PdfPCell(new Phrase(demo.TotalWeight.ToString(), normalFont)) 
                    { 
                        HorizontalAlignment = Element.ALIGN_CENTER, 
                        Padding = 5 
                    });
                    demoTable.AddCell(new PdfPCell(new Phrase(demo.WeightUsed.ToString(), normalFont)) 
                    { 
                        HorizontalAlignment = Element.ALIGN_CENTER, 
                        Padding = 5 
                    });
                }

                document.Add(demoTable);
            }

            document.Close();
            return memoryStream.ToArray();
        }

        public async Task<List<PollAnalyticsDto>> GetAllPollsAnalyticsAsync()
        {
            var polls = await _db.Polls.OrderByDescending(p => p.StartTime).ToListAsync();
            var analyticsList = new List<PollAnalyticsDto>();

            foreach (var poll in polls)
            {
                try
                {
                    var analytics = await GetPollAnalyticsAsync(poll.Id);
                    analyticsList.Add(analytics);
                }
                catch
                {
                    // Skip polls that fail to load
                    continue;
                }
            }

            return analyticsList;
        }
    }
}
