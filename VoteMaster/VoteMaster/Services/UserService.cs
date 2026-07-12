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
        private static readonly Random _rng = new();
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1 to avoid ambiguity

        public UserService(AppDbContext db)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<AppUser>();
        }

        // ── Voter code generation ──────────────────────────────────────────────
        private static string GenerateCode()
        {
            return new string(Enumerable.Range(0, 4)
                .Select(_ => CodeChars[_rng.Next(CodeChars.Length)])
                .ToArray());
        }

        private async Task<string> GenerateUniqueVoterCodeAsync()
        {
            string code;
            int attempts = 0;
            do
            {
                code = GenerateCode();
                attempts++;
                if (attempts > 10_000)
                    throw new InvalidOperationException("Unable to generate a unique voter code — code space exhausted.");
            }
            while (await _db.Users.AnyAsync(u => u.VoterCode == code));
            return code;
        }

        public async Task<AppUser?> AuthenticateAsync(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user is null) return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return result == PasswordVerificationResult.Success ? user : null;
        }

        public Task<AppUser?> GetByUsernameAsync(string username) =>
            _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        public Task<AppUser?> GetByEmailAsync(string email) =>
            _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());

        public Task<AppUser?> GetByIdAsync(int id) =>
            _db.Users.FirstOrDefaultAsync(u => u.Id == id);

        public Task<AppUser?> GetByVoterCodeAsync(string voterCode) =>
            _db.Users.FirstOrDefaultAsync(u => u.VoterCode != null && u.VoterCode.ToLower() == voterCode.ToLower());

        public Task<List<AppUser>> GetAllAsync() =>
            _db.Users.OrderBy(u => u.Username).ToListAsync();

        public Task<List<AppUser>> GetAllForAdminAsync(int adminId) =>
            _db.Users
               .Where(u => u.CreatedByAdminId == adminId)
               .OrderBy(u => u.Username)
               .ToListAsync();

        public async Task<AppUser> CreateAsync(AppUser user, string password)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            user.PlainPassword = password; // stored so admins can view/reset credentials

            // Auto-assign a unique 4-char voter code for Voter accounts
            if (user.Role == "Voter" && string.IsNullOrEmpty(user.VoterCode))
                user.VoterCode = await GenerateUniqueVoterCodeAsync();

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(AppUser user)
        {
            // Ensure existing voters without a code get one assigned on next save
            if (user.Role == "Voter" && string.IsNullOrEmpty(user.VoterCode))
                user.VoterCode = await GenerateUniqueVoterCodeAsync();

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

        public async Task ChangePasswordAsync(int id, string newPassword)
        {
            var user = await _db.Users.FindAsync(id);
            if (user is null) return;
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.PlainPassword = newPassword;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task<int> DeleteAllVotersAsync(int adminId)
        {
            var voters = await _db.Users
                .Where(u => u.Role == "Voter" && u.CreatedByAdminId == adminId)
                .ToListAsync();
            _db.Users.RemoveRange(voters);
            await _db.SaveChangesAsync();
            return voters.Count;
        }
    }
}
