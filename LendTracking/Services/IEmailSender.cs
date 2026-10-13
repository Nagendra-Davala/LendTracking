namespace LendTracking.Services
{
    public interface IEmailSender
    {
        Task SendVerificationEmailAsync(string toEmail, string displayName, string verificationLink, CancellationToken ct = default);

        Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetLink, CancellationToken ct = default);

        /// <summary>Sent instead of a reset link when the account has no password to reset (Google-only).</summary>
        Task SendGoogleOnlyAccountNoticeAsync(string toEmail, string displayName, CancellationToken ct = default);
    }

    /// <summary>Development stub: writes the link to the log instead of sending mail.</summary>
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendVerificationEmailAsync(string toEmail, string displayName, string verificationLink, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "=== EMAIL NOT SENT (dev stub) ===\nTo: {Email} ({Name})\nVerify link: {Link}\n=================================",
                toEmail, displayName, verificationLink);

            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetLink, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "=== EMAIL NOT SENT (dev stub) ===\nTo: {Email} ({Name})\nReset link: {Link}\n=================================",
                toEmail, displayName, resetLink);

            return Task.CompletedTask;
        }

        public Task SendGoogleOnlyAccountNoticeAsync(string toEmail, string displayName, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "=== EMAIL NOT SENT (dev stub) ===\nTo: {Email} ({Name})\nNotice: this account signs in with Google, no password to reset\n=================================",
                toEmail, displayName);

            return Task.CompletedTask;
        }
    }
}
