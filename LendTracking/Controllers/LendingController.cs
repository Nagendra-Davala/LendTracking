using System.Security.Claims;
using LendTracking.Model.Lending;
using LendTracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LendTracking.Controllers
{
    [Route("api/lending")]
    [ApiController]
    [Authorize]
    public class LendingController : ControllerBase
    {
        private readonly LendingService _lendingService;

        public LendingController(LendingService lendingService)
        {
            _lendingService = lendingService;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? direction, [FromQuery] string? status, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();
            return Ok(await _lendingService.GetAllAsync(userId, direction, status, ct));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var record = await _lendingService.GetByIdAsync(userId, id, ct);
            return record is null ? NotFound() : Ok(record);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] LendingRecordRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var created = await _lendingService.CreateAsync(userId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.LendingRecordId }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] LendingRecordRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var updated = await _lendingService.UpdateAsync(userId, id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpPatch("{id:int}/cleared")]
        public async Task<IActionResult> SetCleared(int id, [FromBody] SetClearedRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var updated = await _lendingService.SetClearedAsync(userId, id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            return await _lendingService.DeleteAsync(userId, id, ct) ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/events")]
        public async Task<IActionResult> GetEvents(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var ledger = await _lendingService.GetLedgerAsync(userId, id, ct);
            return ledger is null ? NotFound() : Ok(ledger);
        }

        [HttpPost("{id:int}/events")]
        public async Task<IActionResult> AddEvent(int id, [FromBody] AddPaymentEventRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            try
            {
                var ledger = await _lendingService.AddPaymentEventAsync(userId, id, request, ct);
                return ledger is null ? NotFound() : Ok(ledger);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}/events/{eventId:int}")]
        public async Task<IActionResult> DeleteEvent(int id, int eventId, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            return await _lendingService.DeleteEventAsync(userId, id, eventId, ct) ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/share")]
        public async Task<IActionResult> GetShareLink(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var link = await _lendingService.GetActiveShareLinkAsync(userId, id, ct);
            return link is null ? NotFound() : Ok(link);
        }

        [HttpPost("{id:int}/share")]
        public async Task<IActionResult> CreateShareLink(int id, [FromBody] CreateShareLinkRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var link = await _lendingService.CreateOrUpdateShareLinkAsync(userId, id, request, ct);
            return link is null ? NotFound() : Ok(link);
        }

        [HttpDelete("{id:int}/share")]
        public async Task<IActionResult> RevokeShareLink(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            return await _lendingService.RevokeShareLinkAsync(userId, id, ct) ? NoContent() : NotFound();
        }
    }
}
