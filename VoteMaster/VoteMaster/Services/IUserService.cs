using VoteMaster.Models;

namespace VoteMaster.Services
{
    public interface IUserService
    {
        Task<AppUser?> AuthenticateAsync(string username, string password);
        Task<AppUser?> GetByUsernameAsync(string username);
        Task<AppUser?> GetByIdAsync(int id);
        Task<List<AppUser>> GetAllAsync();
        Task<AppUser> CreateAsync(AppUser user, string password);
        Task UpdateAsync(AppUser user);
        Task DeleteAsync(int id);
    }
}
