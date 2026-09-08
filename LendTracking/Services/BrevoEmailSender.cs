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

        public async Task SendVerificationEmailAsync(string toEmail, string displayName, string verificationLink, CancellationToken ct = default)
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
                subject = "Confirm your LendTracker account",
                htmlContent = BuildHtmlBody(displayName, verificationLink),
                textContent = $"Welcome to LendTracker.\n\nConfirm your email address to activate your account:\n{verificationLink}\n\nThis link is valid for 24 hours. If you didn't sign up, you can ignore this message."
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

            _logger.LogInformation("Verification email queued for {Email}.", toEmail);
        }

        private static string BuildHtmlBody(string displayName, string link) => $"""
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
    }
}
