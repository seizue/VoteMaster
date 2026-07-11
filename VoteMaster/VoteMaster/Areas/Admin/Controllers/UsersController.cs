using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using VoteMaster.Models;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class UsersController : Controller
    {
        private readonly IUserService _users;
        public UsersController(IUserService users) { _users = users; }

        private int CurrentAdminId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        // ── Index ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index() =>
            View(await _users.GetAllForAdminAsync(CurrentAdminId));

        // ── Create ─────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Create() => View(new AppUser { Role = "Voter", Weight = 1 });

        [HttpPost]
        public async Task<IActionResult> Create(string username, string password, string role, int weight, string? email, string? fullName)
        {
            await _users.CreateAsync(
                new AppUser { Username = username, Email = email, Role = role, Weight = weight, FullName = fullName, CreatedByAdminId = CurrentAdminId },
                password);
            return RedirectToAction(nameof(Index));
        }

        // ── Edit ───────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _users.GetByIdAsync(id);
            return user is null ? NotFound() : View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string role, int weight, string? fullName, bool isTestAccount = false)
        {
            var user = await _users.GetByIdAsync(id);
            if (user is null) return NotFound();
            user.Role = role;
            user.Weight = weight;
            user.FullName = fullName;
            user.IsTestAccount = isTestAccount;
            await _users.UpdateAsync(user);
            return RedirectToAction(nameof(Index));
        }

        // ── Change Password (single user) ──────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ChangePassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["EditError"] = "Password cannot be empty.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            await _users.ChangePasswordAsync(id, newPassword);
            TempData["EditSuccess"] = "Password updated successfully.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        // ── Reset All Voter Passwords ──────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ResetAllPasswords(string defaultPassword)
        {
            if (string.IsNullOrWhiteSpace(defaultPassword))
            {
                TempData["ImportError"] = "Default password cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            var voters = (await _users.GetAllForAdminAsync(CurrentAdminId))
                             .Where(u => u.Role == "Voter")
                             .ToList();

            foreach (var voter in voters)
                await _users.ChangePasswordAsync(voter.Id, defaultPassword);

            TempData["ImportResult"] = $"Password reset to \"{defaultPassword}\" for {voters.Count} voter(s).";
            return RedirectToAction(nameof(Index));
        }

        // ── Delete All Voters ──────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteAllVoters()
        {
            var count = await _users.DeleteAllVotersAsync(CurrentAdminId);
            TempData["ImportResult"] = $"{count} voter(s) deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── Delete ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _users.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // ── Download Excel Template ────────────────────────────────────────────
        [HttpGet]
        public IActionResult ExportVotersTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Voters");

            // Header row
            ws.Cell(1, 1).Value = "Name of Shareholder";
            ws.Cell(1, 2).Value = "Number of Shares";

            // Style header
            var header = ws.Range("A1:B1");
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Sample rows
            ws.Cell(2, 1).Value = "Juan Dela Cruz";
            ws.Cell(2, 2).Value = 100;
            ws.Cell(3, 1).Value = "Maria Santos";
            ws.Cell(3, 2).Value = 250;

            ws.Column(1).AdjustToContents();
            ws.Column(2).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "voters_import_template.xlsx");
        }

        // ── Import Voters from Excel ───────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ImportVoters(IFormFile file, string? defaultPassword)
        {
            if (file is null || file.Length == 0)
            {
                TempData["ImportError"] = "Please select an Excel file to import.";
                return RedirectToAction(nameof(Index));
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
            {
                TempData["ImportError"] = "Only .xlsx or .xls files are supported.";
                return RedirectToAction(nameof(Index));
            }

            // Use the provided default password, fall back to "1"
            var password = string.IsNullOrWhiteSpace(defaultPassword) ? "1" : defaultPassword.Trim();

            int created = 0;
            int skipped = 0;
            var errors = new List<string>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.First();

            foreach (var row in ws.RowsUsed())
            {
                // Skip header row
                if (row.RowNumber() == 1) continue;

                var nameCell   = row.Cell(1).GetString().Trim();
                var sharesCell = row.Cell(2).GetString().Trim();

                if (string.IsNullOrWhiteSpace(nameCell)) continue;

                if (!int.TryParse(sharesCell, out int shares) || shares < 1)
                {
                    errors.Add($"Row {row.RowNumber()}: '{sharesCell}' is not a valid number of shares — skipped.");
                    skipped++;
                    continue;
                }

                // Generate a safe username from the shareholder name
                // e.g. "Juan Dela Cruz" → "juandelacruz"
                var baseUsername = new string(
                    nameCell.ToLowerInvariant()
                            .Replace(" ", "")
                            .Where(c => char.IsLetterOrDigit(c))
                            .ToArray());

                if (string.IsNullOrEmpty(baseUsername))
                {
                    errors.Add($"Row {row.RowNumber()}: Could not generate a username from '{nameCell}' — skipped.");
                    skipped++;
                    continue;
                }

                // Deduplicate username if it already exists
                var username = baseUsername;
                var suffix = 1;
                while (await _users.GetByUsernameAsync(username) != null)
                    username = $"{baseUsername}{suffix++}";

                await _users.CreateAsync(
                    new AppUser
                    {
                        Username         = username,
                        Role             = "Voter",
                        Weight           = shares,
                        FullName         = nameCell,   // preserve the original shareholder name
                        CreatedByAdminId = CurrentAdminId
                    },
                    password: password);

                created++;
            }

            TempData["ImportResult"] = $"{created} voter(s) imported successfully.";
            if (skipped > 0)
                TempData["ImportWarnings"] = string.Join("|", errors);

            return RedirectToAction(nameof(Index));
        }

        // ── Export Voters JSON ─────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ExportVotersJson()
        {
            var users = await _users.GetAllForAdminAsync(CurrentAdminId);
            var voters = users.Where(u => u.Role == "Voter").Select(u => u.Username).ToList();
            var json = JsonSerializer.Serialize(voters, new JsonSerializerOptions { WriteIndented = true });
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
                $"voters_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        }
    }
}
