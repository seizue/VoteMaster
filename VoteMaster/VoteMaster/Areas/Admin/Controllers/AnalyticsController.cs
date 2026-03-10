using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsService _analytics;
        private readonly IPollService _polls;

        public AnalyticsController(IAnalyticsService analytics, IPollService polls)
        {
            _analytics = analytics;
            _polls = polls;
        }

        public async Task<IActionResult> Index()
        {
            var allAnalytics = await _analytics.GetAllPollsAnalyticsAsync();
            return View(allAnalytics);
        }

        [HttpGet]
        public async Task<IActionResult> PollDetails(int id)
        {
            try
            {
                var analytics = await _analytics.GetPollAnalyticsAsync(id);
                return View(analytics);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> HistoricalTrends(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-6);
            var end = endDate ?? DateTime.UtcNow;

            var trends = await _analytics.GetHistoricalTrendsAsync(start, end);
            
            ViewBag.StartDate = start.ToString("yyyy-MM-dd");
            ViewBag.EndDate = end.ToString("yyyy-MM-dd");
            
            return View(trends);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(int id)
        {
            try
            {
                var csvData = await _analytics.ExportPollResultsToCsvAsync(id);
                var poll = await _polls.GetPollAsync(id);
                var fileName = $"Poll_{id}_{poll?.Title.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.csv";
                
                return File(csvData, "text/csv", fileName);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int id)
        {
            try
            {
                var pdfData = await _analytics.ExportPollResultsToPdfAsync(id);
                var poll = await _polls.GetPollAsync(id);
                var fileName = $"Poll_{id}_{poll?.Title.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.pdf";
                
                return File(pdfData, "application/pdf", fileName);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }
    }
}
