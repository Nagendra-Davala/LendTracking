using System.ComponentModel.DataAnnotations;

namespace LendTracking.Model.Auth
{
    public class SignUpRequest
    {
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        /// <summary>Optional; falls back to the email's local part.</summary>
        [StringLength(100)]
        public string? DisplayName { get; set; }
    }

    public class SignInRequest
    {
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthenticatedUser
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime TokenExpiresAt { get; set; }
    }

    public class VerifyEmailRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class GoogleSignInRequest
    {
        [Required(ErrorMessage = "Google did not return a credential.")]
        public string IdToken { get; set; } = string.Empty;
    }

    public class ResendVerificationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AuthenticatedUser? User { get; set; }

        /// <summary>Set when the account exists but the email has not been confirmed yet.</summary>
        public bool RequiresEmailVerification { get; set; }

        public static AuthResult Fail(string message) => new() { Success = false, Message = message };
        public static AuthResult Ok(AuthenticatedUser user, string message) => new() { Success = true, Message = message, User = user };
        public static AuthResult PendingVerification(string message) =>
            new() { Success = false, Message = message, RequiresEmailVerification = true };
        public static AuthResult Notice(string message) => new() { Success = true, Message = message };
    }
}
