using Microsoft.EntityFrameworkCore;
using VoteMaster.Data;
using VoteMaster.Models;

namespace VoteMaster.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly AppDbContext _db;

        public AppSettingsService(AppDbContext db) => _db = db;

        public async Task<string?> GetNetworkBaseUrlAsync()
        {
            var row = await _db.AppSettings.FirstOrDefaultAsync();
            return row?.NetworkBaseUrl;
        }

        public async Task SetNetworkBaseUrlAsync(string? url)
        {
            var row = await _db.AppSettings.FirstOrDefaultAsync();
            if (row is null)
            {
                row = new AppSettings { NetworkBaseUrl = url };
                _db.AppSettings.Add(row);
            }
            else
            {
                row.NetworkBaseUrl = url;
                _db.AppSettings.Update(row);
            }
            await _db.SaveChangesAsync();
        }
    }
}
