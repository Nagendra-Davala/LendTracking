using LendTracking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly LendTrackingDbContext _db;

        public UsersController(LendTrackingDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? search, CancellationToken ct)
        {
            var query = _db.Users.Where(u => u.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    u.UserName.ToLower().Contains(term) ||
                    u.DisplayName.ToLower().Contains(term));
            }

            var users = await query
                .OrderBy(u => u.UserName)
                .Take(50)
                .Select(u => new { u.UserId, u.UserName, u.DisplayName })
                .ToListAsync(ct);

            return Ok(users);
        }
    }
}
