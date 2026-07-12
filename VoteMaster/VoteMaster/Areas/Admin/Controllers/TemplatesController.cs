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
        public async Task<IActionResult> Create(string name, int pollId, bool includeSignature, IFormFile? headerImage)
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
                HeaderImagePath = imagePath,
                IncludeSignature = includeSignature
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
        public async Task<IActionResult> Edit(int id, string name, int pollId, bool includeSignature, IFormFile? headerImage)
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
            template.IncludeSignature = includeSignature;

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
        public async Task<IActionResult> Ticket(int id, string? baseUrl)
        {
            var template = await _db.TicketTemplates
                .Include(t => t.Poll)
                    .ThenInclude(p => p!.Options)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (template is null) return NotFound();

            // Build the voting URL from custom baseUrl or fallback to request host
            var baseUrlToUse = string.IsNullOrWhiteSpace(baseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : baseUrl;
            var voteUrl = $"{baseUrlToUse}/Client/Vote/{template.PollId}";

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
        public async Task<IActionResult> ExportPdf(int id, string? baseUrl)
        {
            var template = await _db.TicketTemplates
                .Include(t => t.Poll)
                    .ThenInclude(p => p!.Options)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (template is null) return NotFound();

            var initials = string.Concat((template.Poll?.Title ?? template.Name)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0])));
            var ticketCode = $"{initials}-{template.CreatedAt:yyyyMMdd}";

            // Build the voting URL from custom baseUrl or fallback to request host
            var baseUrlToUse = string.IsNullOrWhiteSpace(baseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : baseUrl;
            var voteUrl = $"{baseUrlToUse}/Client/Vote/{template.PollId}";
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(voteUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(6);
            var qrBase64 = Convert.ToBase64String(qrBytes);

            // Load voters
            var allUsers = await _userService.GetAllAsync();
            var voters = allUsers.Where(u => u.Role != "Admin").ToList();

            // Header image as base64 so it works in standalone HTML
            string headerImgTag = "";
            if (!string.IsNullOrEmpty(template.HeaderImagePath))
            {
                var imgPath = Path.Combine(_env.WebRootPath, template.HeaderImagePath.TrimStart('/'));
                if (System.IO.File.Exists(imgPath))
                {
                    var imgBytes = await System.IO.File.ReadAllBytesAsync(imgPath);
                    var ext = Path.GetExtension(imgPath).TrimStart('.').ToLower();
                    var mime = ext == "png" ? "image/png" : "image/jpeg";
                    var b64 = Convert.ToBase64String(imgBytes);
                    headerImgTag = $"<div class=\"ticket-header-wrap\"><img src=\"data:{mime};base64,{b64}\" /></div>";
                }
            }
            else
            {
                headerImgTag = $"<div class=\"ticket-header-placeholder\">{System.Web.HttpUtility.HtmlEncode(template.Poll?.Title ?? template.Name)}</div>";
            }

            var pollTitle = System.Web.HttpUtility.HtmlEncode(template.Poll?.Title ?? template.Name);
            var pollDesc  = System.Web.HttpUtility.HtmlEncode(template.Poll?.Description ?? "");

            // Build pages of 6 tickets
            int perPage = 6;
            var pages = voters
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / perPage)
                .Select(g => g.Select(x => x.v).ToList())
                .ToList();

            var pagesHtml = new System.Text.StringBuilder();
            foreach (var page in pages)
            {
                pagesHtml.AppendLine("<div class=\"a4-page\">");
                for (int r = 0; r < 3; r++)
                {
                    pagesHtml.AppendLine("<div class=\"ticket-row\">");
                    for (int c = 0; c < 2; c++)
                    {
                        int idx = r * 2 + c;
                        if (idx >= page.Count)
                        {
                            pagesHtml.AppendLine("<div class=\"ticket\" style=\"border:none;background:transparent;\"></div>");
                            break;
                        }
                        var voter = page[idx];
                        var uname = System.Web.HttpUtility.HtmlEncode(voter.Username);
                        var signatureRow = template.IncludeSignature
                            ? "<div class=\"ticket-field-row\"><span class=\"ticket-field-label\">Signature:</span><div class=\"ticket-field-line\"></div></div>"
                            : "";

                        pagesHtml.AppendLine($@"
                        <div class=""ticket"">
                            {headerImgTag}
                            <div class=""ticket-title-strip"">
                                <div class=""ticket-poll-title"">{pollTitle}</div>
                                {(string.IsNullOrEmpty(pollDesc) ? "" : $"<div class=\"ticket-poll-desc\">{pollDesc}</div>")}
                            </div>
                            <div class=""ticket-qr-section"">
                                <div class=""ticket-scan-label"">Scan to Vote</div>
                                <img class=""ticket-qr-img"" src=""data:image/png;base64,{qrBase64}"" />
                                <div class=""ticket-scan-sub"">{System.Web.HttpUtility.HtmlEncode(voteUrl)}</div>
                            </div>
                            <div class=""ticket-fields"">
                                <div class=""ticket-field-row"">
                                    <span class=""ticket-field-label"">Username:</span>
                                    <div class=""ticket-field-value usercode"">{uname}</div>
                                </div>
                                {signatureRow}
                            </div>
                            <div class=""ticket-code-strip"">
                                <span class=""ticket-code-text"">Code: {ticketCode}</span>
                                <span class=""ticket-date-text"">{template.CreatedAt.ToLocalTime():MMM dd, yyyy}</span>
                            </div>
                        </div>");
                    }
                    pagesHtml.AppendLine("</div>"); // ticket-row
                }
                pagesHtml.AppendLine("</div>"); // a4-page
            }

            var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{ background: white; font-family: 'Inter', Arial, sans-serif; }}
  .a4-page {{
      width: 794px; height: 1123px;
      padding: 6px;
      display: flex; flex-direction: column; gap: 5px;
      overflow: hidden;
      page-break-after: always; break-after: page;
  }}
  .ticket-row {{
      display: flex; flex-direction: row; gap: 6px;
      height: 367px; flex-shrink: 0; overflow: hidden;
  }}
  .ticket {{
      flex: 1; min-width: 0;
      border: 1.5px solid #1e293b; border-radius: 4px;
      display: flex; flex-direction: column; overflow: hidden;
      background: white;
  }}
  .ticket-header-wrap {{ flex-shrink:0; overflow:hidden; line-height:0; }}
  .ticket-header-wrap img {{ width:100%; height:auto; display:block; max-height:55px; object-fit:cover; object-position:top; }}
  .ticket-header-placeholder {{
      height: 24px; flex-shrink: 0;
      background: linear-gradient(135deg,#0f172a,#1e3a5f);
      display: flex; align-items: center; justify-content: center;
      color: white; font-size: 7pt; font-weight: 700;
  }}
  .ticket-title-strip {{ padding: 2px 5px; flex-shrink: 0; border-bottom: 1px dashed #cbd5e1; }}
  .ticket-poll-title {{ font-size: 7pt; font-weight: 700; color: #0f172a; line-height: 1.2; }}
  .ticket-poll-desc  {{ font-size: 5pt; color: #64748b; line-height: 1.2; }}
  .ticket-qr-section {{
      flex: 1; display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      padding: 3px 6px; gap: 2px;
  }}
  .ticket-scan-label {{ font-size: 7pt; font-weight: 800; text-transform: uppercase; letter-spacing: 0.1em; color: #0f172a; }}
  .ticket-qr-img {{ width: 160px; height: 160px; display: block; }}
  .ticket-scan-sub {{ font-size: 5.5pt; color: #0f172a; font-weight: 600; font-family: monospace; text-align: center; word-break: break-all; }}
  .ticket-fields {{
      padding: 4px 7px 5px; flex-shrink: 0;
      border-top: 2px solid #0f172a; background: #f0f9ff;
      display: flex; flex-direction: column; gap: 6px;
  }}
  .ticket-field-row {{ display: flex; align-items: flex-end; gap: 4px; }}
  .ticket-field-label {{ font-size: 8pt; font-weight: 700; color: #475569; white-space: nowrap; flex-shrink: 0; padding-bottom: 1px; }}
  .ticket-field-line {{ flex: 1; border-bottom: 1.5px solid #0f172a; height: 14px; min-width: 0; }}
  .ticket-field-value {{ font-size: 8pt; font-weight: 600; color: #0f172a; border-bottom: 1.5px solid #0f172a; padding-bottom: 1px; flex: 1; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}
  .ticket-field-value.usercode {{ color: #e11d48; font-weight: 700; }}
  .ticket-code-strip {{ padding: 2px 5px; background: #f8fafc; border-top: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; flex-shrink: 0; }}
  .ticket-code-text {{ font-size: 5pt; font-weight: 700; color: #64748b; letter-spacing: 0.05em; }}
  .ticket-date-text {{ font-size: 5pt; color: #94a3b8; }}
</style>
</head>
<body>
{pagesHtml}
</body>
</html>";

            var browser = await _browserService.GetBrowserAsync();
            var puppeteerPage = await browser.NewPageAsync();

            try
            {
                await puppeteerPage.SetViewportAsync(new PuppeteerSharp.ViewPortOptions
                {
                    Width = 794,
                    Height = 1123,
                    DeviceScaleFactor = 1
                });

                await puppeteerPage.SetContentAsync(html, new PuppeteerSharp.NavigationOptions
                {
                    WaitUntil = new[] { PuppeteerSharp.WaitUntilNavigation.Networkidle0 }
                });

                var pdfBytes = await puppeteerPage.PdfDataAsync(new PuppeteerSharp.PdfOptions
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
                await puppeteerPage.CloseAsync();
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
