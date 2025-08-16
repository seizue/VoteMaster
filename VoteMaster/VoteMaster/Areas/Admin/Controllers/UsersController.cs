using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public async Task<IActionResult> Index() => View(await _users.GetAllAsync());

        [HttpGet]
        public IActionResult Create() => View(new AppUser { Role = "Voter", Weight = 1 });

        [HttpPost]
        public async Task<IActionResult> Create(string username, string password, string role, int weight)
        {
            await _users.CreateAsync(new AppUser { Username = username, Role = role, Weight = weight }, password);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _users.GetByIdAsync(id);
            return user is null ? NotFound() : View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string role, int weight)
        {
            var user = await _users.GetByIdAsync(id);
            if (user is null) return NotFound();
            user.Role = role;
            user.Weight = weight;
            await _users.UpdateAsync(user);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _users.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
