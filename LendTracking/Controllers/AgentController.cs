using System.Security.Claims;
using System.Text.Json;
using LendTracking.Model.Agent;
using LendTracking.Model.Lending;
using LendTracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LendTracking.Controllers
{
    [Route("api/agent")]
    [ApiController]
    [Authorize]
    public class AgentController : ControllerBase
    {
        private readonly AgentService _agentService;
        private readonly LendingService _lendingService;
        private readonly PendingActionStore _pendingActions;
        private readonly SpeechService _speechService;

        public AgentController(
            AgentService agentService,
            LendingService lendingService,
            PendingActionStore pendingActions,
            SpeechService speechService)
        {
            _agentService = agentService;
            _lendingService = lendingService;
            _pendingActions = pendingActions;
            _speechService = speechService;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpPost("chat")]
        public async Task Chat([FromBody] AgentChatRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                await foreach (var evt in _agentService.StreamChatAsync(userId, request.Messages, request.LanguageHint, ct))
                {
                    var line = $"event: {evt.Event}\ndata: {JsonSerializer.Serialize(evt.Data)}\n\n";
                    await Response.WriteAsync(line, ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Last-resort net: anything that escaped AgentService's own error handling
                // still needs to reach the client as an SSE frame, not an opaque 500.
                var line = $"event: error\ndata: {JsonSerializer.Serialize(new { message = ex.Message })}\n\n";
                await Response.WriteAsync(line, ct);
                await Response.Body.FlushAsync(ct);
            }
        }

        [HttpPost("transcribe")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Transcribe(IFormFile? audio, [FromForm] string? language, CancellationToken ct)
        {
            if (CurrentUserId is null) return Unauthorized();
            if (audio is null || audio.Length == 0) return BadRequest(new { message = "No audio received." });

            try
            {
                await using var stream = audio.OpenReadStream();
                var result = await _speechService.TranscribeAsync(stream, audio.FileName, audio.ContentType, language, ct);
                return Ok(new { text = result.Text, language = result.Language });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
        }

        [HttpPost("actions/{token}/confirm")]
        public async Task<IActionResult> Confirm(string token, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var pending = _pendingActions.Take(token, userId);
            if (pending is null) return NotFound(new { message = "This proposal has expired or was already handled." });

            switch (pending.Action)
            {
                case "ProposeAddLending":
                {
                    var payload = JsonSerializer.Deserialize<ProposedAddLendingPayload>(pending.PayloadJson, JsonOpts)!;
                    var created = await _lendingService.CreateAsync(userId, new LendingRecordRequest
                    {
                        CounterpartyName = payload.CounterpartyName,
                        Direction = payload.Direction,
                        Principal = payload.Principal,
                        AnnualRatePercent = payload.AnnualRatePercent,
                        GivenDate = payload.GivenDate,
                        Notes = payload.Notes
                    }, ct);
                    return Ok(created);
                }
                case "ProposeSetCleared":
                {
                    var payload = JsonSerializer.Deserialize<ProposedSetClearedPayload>(pending.PayloadJson, JsonOpts)!;
                    var updated = await _lendingService.SetClearedAsync(userId, payload.LendingRecordId,
                        new SetClearedRequest { IsCleared = payload.IsCleared }, ct);
                    return updated is null ? NotFound() : Ok(updated);
                }
                case "ProposeDeleteLending":
                {
                    var payload = JsonSerializer.Deserialize<ProposedDeleteLendingPayload>(pending.PayloadJson, JsonOpts)!;
                    var deleted = await _lendingService.DeleteAsync(userId, payload.LendingRecordId, ct);
                    return deleted ? NoContent() : NotFound();
                }
                default:
                    return BadRequest(new { message = "Unknown proposed action." });
            }
        }

        [HttpPost("actions/{token}/cancel")]
        public IActionResult Cancel(string token)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            _pendingActions.Discard(token, userId);
            return NoContent();
        }

        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        private record ProposedAddLendingPayload(
            string CounterpartyName,
            string Direction,
            decimal Principal,
            decimal AnnualRatePercent,
            DateOnly GivenDate,
            string? Notes);

        private record ProposedSetClearedPayload(int LendingRecordId, bool IsCleared);

        private record ProposedDeleteLendingPayload(int LendingRecordId);
    }
}
