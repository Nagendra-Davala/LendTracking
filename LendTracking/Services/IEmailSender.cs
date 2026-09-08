namespace LendTracking.Services
{
    public interface IEmailSender
    {
        Task SendVerificationEmailAsync(string toEmail, string displayName, string verificationLink, CancellationToken ct = default);
    }

    /// <summary>Development stub: writes the verification link to the log instead of sending mail.</summary>
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
    }
}
