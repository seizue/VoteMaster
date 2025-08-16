using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VoteMaster.Data;
using VoteMaster.Models;

namespace VoteMaster.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<AppUser> _passwordHasher;

        public UserService(AppDbContext db)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<AppUser>();
        }

        public async Task<AppUser?> AuthenticateAsync(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user is null) return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return result == PasswordVerificationResult.Success ? user : null;
        }

        public Task<AppUser?> GetByUsernameAsync(string username) =>
            _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        public Task<AppUser?> GetByIdAsync(int id) =>
            _db.Users.FirstOrDefaultAsync(u => u.Id == id);

        public Task<List<AppUser>> GetAllAsync() =>
            _db.Users.OrderBy(u => u.Username).ToListAsync();

        public async Task<AppUser> CreateAsync(AppUser user, string password)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(AppUser user)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u != null)
            {
                _db.Users.Remove(u);
                await _db.SaveChangesAsync();
            }
        }
    }
}
