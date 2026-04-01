using PuppeteerSharp;

namespace VoteMaster.Services
{
    public class BrowserService : IAsyncDisposable
    {
        private IBrowser? _browser;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser != null && !_browser.IsClosed)
                return _browser;

            await _lock.WaitAsync();
            try
            {
                if (_browser == null || _browser.IsClosed)
                {
                    // Download Chromium once if needed
                    var fetcher = new BrowserFetcher();
                    await fetcher.DownloadAsync();

                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true,
                        Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
                    });
                }
            }
            finally
            {
                _lock.Release();
            }

            return _browser;
        }

        public async ValueTask DisposeAsync()
        {
            if (_browser != null && !_browser.IsClosed)
                await _browser.CloseAsync();
        }
    }
}
