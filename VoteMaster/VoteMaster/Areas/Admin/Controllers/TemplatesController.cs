using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using VoteMaster.Data;
using VoteMaster.Models;
using VoteMaster.Services;

namespace VoteMaster.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOnly")]
    public class TemplatesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPollService _polls;
        private readonly IWebHostEnvironment _env;
        private readonly IUserService _userService;
        private readonly BrowserService _browserService;

        public TemplatesController(AppDbContext db, IPollService polls, IWebHostEnvironment env, IUserService userService, BrowserService browserService)
        {
            _db = db;
            _polls = polls;
            _env = env;
            _userService = userService;
            _browserService = browserService;
        }

        public async Task<IActionResult> Index()
        {
            var templates = await _db.TicketTemplates
                .Include(t => t.Poll)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(templates);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Polls = await _polls.GetAllPollsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, int pollId, IFormFile? headerImage)
        {
            if (string.IsNullOrWhiteSpace(name) || pollId == 0)
            {
                ModelState.AddModelError("", "Name and Poll are required.");
                ViewBag.Polls = await _polls.GetAllPollsAsync();
                return View();
            }

            string? imagePath = null;
            if (headerImage is { Length: > 0 })
            {
                var ext = Path.GetExtension(headerImage.FileName).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                {
                    ModelState.AddModelError("", "Only PNG and JPG images are allowed.");
                    ViewBag.Polls = await _polls.GetAllPollsAsync();
                    return View();
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "templates");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"{Guid.NewGuid()}{ext}";
                var fullPath = Path.Combine(uploadsDir, fileName);
                using var stream = System.IO.File.Create(fullPath);
                await headerImage.CopyToAsync(stream);
                imagePath = $"/uploads/templates/{fileName}";
            }

            var template = new TicketTemplate
            {
                Name = name,
                PollId = pollId,
                HeaderImagePath = imagePath
            };

            _db.TicketTemplates.Add(template);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Ticket), new { id = template.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var template = await _db.TicketTemplates.FindAsync(id);
            if (template is null) return NotFound();
            ViewBag.Polls = await _polls.GetAllPollsAsync();
            return View(template);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, int pollId, IFormFile? headerImage)
        {
            var template = await _db.TicketTemplates.FindAsync(id);
            if (template is null) return NotFound();

            if (string.IsNullOrWhiteSpace(name) || pollId == 0)
            {
                ModelState.AddModelError("", "Name and Poll are required.");
                ViewBag.Polls = await _polls.GetAllPollsAsync();
                return View(template);
            }

            template.Name = name;
            template.PollId = pollId;

            if (headerImage is { Length: > 0 })
            {
                var ext = Path.GetExtension(headerImage.FileName).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                {
                    ModelState.AddModelError("", "Only PNG and JPG images are allowed.");
                    ViewBag.Polls = await _polls.GetAllPollsAsync();
                    return View(template);
                }
                if (!string.IsNullOrEmpty(template.HeaderImagePath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, template.HeaderImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "templates");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"{Guid.NewGuid()}{ext}";
                var fullPath = Path.Combine(uploadsDir, fileName);
                using var stream = System.IO.File.Create(fullPath);
                await headerImage.CopyToAsync(stream);
                template.HeaderImagePath = $"/uploads/templates/{fileName}";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Renders the printable ticket vote form
        public async Task<IActionResult> Ticket(int id)
        {
            var template = await _db.TicketTemplates
                .Include(t => t.Poll)
                    .ThenInclude(p => p!.Options)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (template is null) return NotFound();

            // Build the voting URL
            var voteUrl = $"{Request.Scheme}://{Request.Host}/Client/Vote/{template.PollId}";

            // Generate QR code as base64 PNG
            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(voteUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(6);
            var qrBase64 = Convert.ToBase64String(qrBytes);

            // Load all voters (non-admin users)
            var allUsers = await _userService.GetAllAsync();
            var voters = allUsers.Where(u => u.Role != "Admin").ToList();

            ViewBag.VoteUrl = voteUrl;
            ViewBag.QrCodeBase64 = qrBase64;
            ViewBag.Voters = voters;

            return View(template);
        }

         [HttpGet]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var template = await _db.TicketTemplates
                .Include(t => t.Poll)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (template is null) return NotFound();

            var initials = string.Concat((template.Poll?.Title ?? template.Name)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0])));
            var ticketCode = $"{initials}-{template.CreatedAt:yyyyMMdd}";

            var ticketUrl = $"{Request.Scheme}://{Request.Host}/Admin/Templates/Ticket/{id}";
            var cookieHeader = Request.Headers["Cookie"].ToString();

            // Reuse the singleton browser — no cold start after first request
            var browser = await _browserService.GetBrowserAsync();
            var page = await browser.NewPageAsync();

            try
            {
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    var cookies = cookieHeader.Split(';')
                        .Select(c => c.Trim().Split('=', 2))
                        .Where(p => p.Length == 2)
                        .Select(p => new PuppeteerSharp.CookieParam
                        {
                            Name   = p[0].Trim(),
                            Value  = p[1].Trim(),
                            Domain = Request.Host.Host,
                            Path   = "/"
                        }).ToArray();
                    await page.SetCookieAsync(cookies);
                }

                await page.GoToAsync(ticketUrl, waitUntil: new[]
                {
                    PuppeteerSharp.WaitUntilNavigation.Networkidle2
                });

                // Set viewport to A4 width at 96dpi so mm units resolve correctly
                await page.SetViewportAsync(new PuppeteerSharp.ViewPortOptions
                {
                    Width = 794,   // 210mm at 96dpi
                    Height = 1123, // 297mm at 96dpi
                    DeviceScaleFactor = 1
                });

                var pdfBytes = await page.PdfDataAsync(new PuppeteerSharp.PdfOptions
                {
                    Format = PuppeteerSharp.Media.PaperFormat.A4,
                    PrintBackground = true,
                    MarginOptions = new PuppeteerSharp.Media.MarginOptions
                    {
                        Top = "0mm", Bottom = "0mm", Left = "0mm", Right = "0mm"
                    }
                });

                return File(pdfBytes, "application/pdf", $"Tickets-{ticketCode}.pdf");
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var template = await _db.TicketTemplates.FindAsync(id);
            if (template is not null)
            {
                // Remove header image file if exists
                if (!string.IsNullOrEmpty(template.HeaderImagePath))
                {
                    var fullPath = Path.Combine(_env.WebRootPath, template.HeaderImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                        System.IO.File.Delete(fullPath);
                }
                _db.TicketTemplates.Remove(template);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
