using System.Security.Claims;
using LendTracking.Model.Auth;
using LendTracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LendTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly bool _passwordAuthEnabled;

        public AuthController(AuthService authService, IConfiguration config)
        {
            _authService = authService;
            _passwordAuthEnabled = config.GetValue("Auth:EnablePasswordAuth", true);
        }

        private ObjectResult? PasswordAuthDisabled() =>
            _passwordAuthEnabled
                ? null
                : StatusCode(StatusCodes.Status403Forbidden,
                    AuthResult.Fail("Email and password sign-in is currently unavailable. Please continue with Google."));

        [HttpPost("signup")]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResult>> SignUp([FromBody] SignUpRequest request, CancellationToken ct)
        {
            if (PasswordAuthDisabled() is { } blocked) return blocked;

            var result = await _authService.SignUpAsync(request, ct);

            // A pending-verification signup succeeded; it just cannot hand out a token yet.
            return result.Success || result.RequiresEmailVerification ? Ok(result) : BadRequest(result);
        }

        [HttpPost("signin")]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<AuthResult>> SignIn([FromBody] SignInRequest request, CancellationToken ct)
        {
            if (PasswordAuthDisabled() is { } blocked) return blocked;

            var result = await _authService.SignInAsync(request, ct);

            if (result.Success)
            {
                return Ok(result);
            }

            return result.RequiresEmailVerification
                ? StatusCode(StatusCodes.Status403Forbidden, result)
                : Unauthorized(result);
        }

        [HttpPost("verify-email")]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResult>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
        {
            if (PasswordAuthDisabled() is { } blocked) return blocked;

            var result = await _authService.VerifyEmailAsync(request.Token, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("resend-verification")]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
        public async Task<ActionResult<AuthResult>> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
        {
            if (PasswordAuthDisabled() is { } blocked) return blocked;

            return Ok(await _authService.ResendVerificationAsync(request.Email, ct));
        }

        [HttpPost("google")]
        public async Task<ActionResult<AuthResult>> Google(
            [FromBody] GoogleSignInRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _authService.SignInWithGoogleAsync(request.IdToken, ct);
                return result.Success ? Ok(result) : Unauthorized(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _authService.GetByIdAsync(userId, ct);
            if (user is null)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                user.UserId,
                user.UserName,
                user.DisplayName,
                user.Email
            });
        }
    }
}
