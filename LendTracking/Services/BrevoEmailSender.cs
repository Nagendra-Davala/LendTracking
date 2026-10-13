using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LendTracking.Services
{
    /// <summary>Sends through Brevo's transactional API; no SDK needed, it is a single POST.</summary>
    public class BrevoEmailSender : IEmailSender
    {
        private const string SendEndpoint = "https://api.brevo.com/v3/smtp/email";

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<BrevoEmailSender> _logger;

        public BrevoEmailSender(HttpClient http, IConfiguration config, ILogger<BrevoEmailSender> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public Task SendVerificationEmailAsync(string toEmail, string displayName, string verificationLink, CancellationToken ct = default) =>
            SendAsync(
                toEmail,
                displayName,
                "Confirm your LendTracker account",
                BuildVerificationHtmlBody(displayName, verificationLink),
                $"Welcome to LendTracker.\n\nConfirm your email address to activate your account:\n{verificationLink}\n\nThis link is valid for 24 hours. If you didn't sign up, you can ignore this message.",
                ct);

        public Task SendPasswordResetEmailAsync(string toEmail, string displayName, string resetLink, CancellationToken ct = default) =>
            SendAsync(
                toEmail,
                displayName,
                "Reset your LendTracker password",
                BuildResetHtmlBody(displayName, resetLink),
                $"Reset your LendTracker password.\n\nClick this link to choose a new password:\n{resetLink}\n\nThis link is valid for 1 hour. If you didn't ask for this, you can ignore this message.",
                ct);

        public Task SendGoogleOnlyAccountNoticeAsync(string toEmail, string displayName, CancellationToken ct = default) =>
            SendAsync(
                toEmail,
                displayName,
                "About your LendTracker account",
                BuildGoogleOnlyHtmlBody(displayName),
                "Someone requested a password reset for this LendTracker account, but it was created with Google Sign-In and has no password. Use the \"Continue with Google\" button to sign in instead.",
                ct);

        private async Task SendAsync(
            string toEmail,
            string displayName,
            string subject,
            string htmlContent,
            string textContent,
            CancellationToken ct)
        {
            var section = _config.GetSection("Email:Brevo");
            var apiKey = section["ApiKey"]
                ?? throw new InvalidOperationException("Email:Brevo:ApiKey is not configured.");
            var fromAddress = section["FromAddress"]
                ?? throw new InvalidOperationException("Email:Brevo:FromAddress is not configured.");
            var fromName = section["FromName"] ?? "LendTracker";

            var payload = new
            {
                sender = new { email = fromAddress, name = fromName },
                to = new[] { new { email = toEmail, name = displayName } },
                subject,
                htmlContent,
                textContent
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Brevo rejected the message for {Email}: {Status} {Body}",
                    toEmail, response.StatusCode, body);
                throw new InvalidOperationException($"Brevo returned {(int)response.StatusCode}.");
            }

            _logger.LogInformation("Email \"{Subject}\" queued for {Email}.", subject, toEmail);
        }

        private static string BuildVerificationHtmlBody(string displayName, string link) => $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#111827">
              <h1 style="margin:0 0 8px;font-size:22px;color:#111827">Confirm your email</h1>
              <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#4b5563">
                Hi {System.Net.WebUtility.HtmlEncode(displayName)}, welcome to LendTracker.
                Click the button below to activate your account.
              </p>
              <a href="{link}" style="display:inline-block;padding:12px 22px;background:#6d28d9;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px">
                Activate my account
              </a>
              <p style="margin:24px 0 0;font-size:13px;line-height:1.6;color:#6b7280">
                This link is valid for 24 hours. If the button doesn't work, paste this into your browser:<br>
                <span style="word-break:break-all;color:#6d28d9">{link}</span>
              </p>
              <p style="margin:20px 0 0;font-size:13px;color:#9ca3af">
                Didn't sign up? You can safely ignore this email.
              </p>
            </div>
            """;

        private static string BuildResetHtmlBody(string displayName, string link) => $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#111827">
              <h1 style="margin:0 0 8px;font-size:22px;color:#111827">Reset your password</h1>
              <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#4b5563">
                Hi {System.Net.WebUtility.HtmlEncode(displayName)}, we got a request to reset your LendTracker password.
                Click the button below to choose a new one.
              </p>
              <a href="{link}" style="display:inline-block;padding:12px 22px;background:#6d28d9;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px">
                Reset my password
              </a>
              <p style="margin:24px 0 0;font-size:13px;line-height:1.6;color:#6b7280">
                This link is valid for 1 hour. If the button doesn't work, paste this into your browser:<br>
                <span style="word-break:break-all;color:#6d28d9">{link}</span>
              </p>
              <p style="margin:20px 0 0;font-size:13px;color:#9ca3af">
                Didn't ask for this? You can safely ignore this email — your password won't change.
              </p>
            </div>
            """;

        private static string BuildGoogleOnlyHtmlBody(string displayName) => $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#111827">
              <h1 style="margin:0 0 8px;font-size:22px;color:#111827">About your account</h1>
              <p style="margin:0 0 16px;font-size:15px;line-height:1.6;color:#4b5563">
                Hi {System.Net.WebUtility.HtmlEncode(displayName)}, someone just requested a password reset for this LendTracker account.
              </p>
              <p style="margin:0 0 16px;font-size:15px;line-height:1.6;color:#4b5563">
                This account was created with Google Sign-In and has no password — use the
                <strong>&quot;Continue with Google&quot;</strong> button on the sign-in page instead.
              </p>
              <p style="margin:20px 0 0;font-size:13px;color:#9ca3af">
                If this wasn't you, you can safely ignore this email.
              </p>
            </div>
            """;
    }
}
