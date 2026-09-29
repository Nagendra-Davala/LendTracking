using LendTracking.Services;
using Microsoft.AspNetCore.Mvc;

namespace LendTracking.Controllers
{
    /// <summary>
    /// Deliberately unauthenticated — anyone holding a valid, unexpired, non-revoked token can
    /// view that one lending record's ledger. No [Authorize] anywhere in this controller by design.
    /// </summary>
    [Route("api/public/share")]
    [ApiController]
    public class PublicShareController : ControllerBase
    {
        private readonly LendingService _lendingService;

        public PublicShareController(LendingService lendingService)
        {
            _lendingService = lendingService;
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
        {
            var share = await _lendingService.GetPublicShareAsync(token, ct);
            return share is null ? NotFound() : Ok(share);
        }
    }
}
