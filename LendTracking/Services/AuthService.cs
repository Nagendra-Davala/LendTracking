using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LendTracking.Data;
using LendTracking.Model.Auth;
using LendTracking.Model.Entities;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace LendTracking.Services
{
    public class AuthService
    {
        private readonly LendTrackingDbContext _db;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AuthService> _logger;

        public AuthService(LendTrackingDbContext db, IConfiguration config, IEmailSender emailSender, ILogger<AuthService> logger)
        {
            _db = db;
            _config = config;
            _emailSender = emailSender;
            _logger = logger;
        }

        private TimeSpan VerificationTokenLifetime =>
            TimeSpan.FromMinutes(_config.GetValue("Auth:VerificationLinkMinutes", 1440));

        public async Task<AuthResult> SignUpAsync(SignUpRequest request, CancellationToken ct = default)
        {
            // Stored lower-cased so the unique indexes enforce case-insensitive identity.
            var email = request.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            {
                return AuthResult.Fail("An account already exists with that email address.");
            }

            var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == email, ct);
            var rawToken = NewToken();

            if (pending is null)
            {
                pending = new PendingRegistration { Email = email, CreatedAt = DateTime.UtcNow };
                _db.PendingRegistrations.Add(pending);
            }

            // Signing up again before confirming simply replaces the previous attempt.
            pending.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            pending.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? email.Split('@')[0]
                : request.DisplayName.Trim();
            pending.TokenHash = HashToken(rawToken);
            pending.ExpiresAt = DateTime.UtcNow.Add(VerificationTokenLifetime);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                _db.Entry(pending).State = EntityState.Detached;
                return AuthResult.Fail("A confirmation link was already sent to that address. Please check your inbox.");
            }

            await SendVerificationLinkAsync(email, pending.DisplayName, rawToken, ct);

            return AuthResult.PendingVerification(
                $"Almost there. We sent a confirmation link to {email}. Your account is created once you click it.");
        }

        public async Task<AuthResult> SignInWithGoogleAsync(string idToken, CancellationToken ct = default)
        {
            var clientId = _config["Google:ClientId"]
                ?? throw new InvalidOperationException("Google:ClientId is not configured.");

            GoogleJsonWebSignature.Payload payload;
            try
            {
                // Verifies Google's signature, the audience, and expiry. Never trust the raw token.
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Covers bad signatures, wrong audience, and tokens that are not even well-formed.
                _logger.LogWarning(ex, "Google ID token validation failed.");
                return AuthResult.Fail("That Google sign-in could not be verified. Please try again.");
            }

            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
            {
                return AuthResult.Fail("Your Google account does not have a verified email address.");
            }

            var email = payload.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == payload.Subject, ct);

            if (user is null)
            {
                // Google has proven ownership of this address, so linking to an existing account is safe.
                user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

                if (user is not null)
                {
                    user.GoogleSubjectId = payload.Subject;
                }
                else
                {
                    user = new AppUser
                    {
                        UserName = await GenerateUniqueUserNameAsync(email, ct),
                        DisplayName = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name,
                        Email = email,
                        GoogleSubjectId = payload.Subject,
                        PasswordHash = null,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _db.Users.Add(user);
                }

                // Any half-finished email signup for this address is now redundant.
                var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == email, ct);
                if (pending is not null)
                {
                    // Honour the password they already chose so both sign-in methods keep working.
                    user.PasswordHash ??= pending.PasswordHash;
                    _db.PendingRegistrations.Remove(pending);
                }

                await _db.SaveChangesAsync(ct);
            }

            if (!user.IsActive)
            {
                return AuthResult.Fail("This account has been deactivated.");
            }

            return AuthResult.Ok(BuildAuthenticatedUser(user), "Signed in with Google.");
        }

        private async Task<string> GenerateUniqueUserNameAsync(string email, CancellationToken ct)
        {
            var baseName = new string(email.Split('@')[0]
                .Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
                .ToArray())
                .ToLowerInvariant();

            if (baseName.Length < 3)
            {
                baseName = $"user{baseName}";
            }

            var candidate = baseName;
            var suffix = 1;

            while (await _db.Users.AnyAsync(u => u.UserName == candidate, ct))
            {
                candidate = $"{baseName}{++suffix}";
            }

            return candidate;
        }

        public async Task<AuthResult> SignInAsync(SignInRequest request, CancellationToken ct = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user is null)
            {
                // Give the unconfirmed signup a way forward instead of a dead-end error.
                // Expiry is ignored here so an expired link can still be resent.
                if (await _db.PendingRegistrations.AnyAsync(p => p.Email == email, ct))
                {
                    return AuthResult.PendingVerification(
                        "Your account isn't active yet. Please click the confirmation link we emailed you.");
                }

                return AuthResult.Fail("Incorrect email or password.");
            }

            if (user.IsActive && user.PasswordHash is null)
            {
                return AuthResult.Fail("This account was created with Google. Use the \"Continue with Google\" button.");
            }

            // Same message for unknown email and wrong password so accounts cannot be enumerated.
            if (!user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return AuthResult.Fail("Incorrect email or password.");
            }

            return AuthResult.Ok(BuildAuthenticatedUser(user), "Signed in successfully.");
        }

        public async Task<AuthResult> VerifyEmailAsync(string rawToken, CancellationToken ct = default)
        {
            var hash = HashToken(rawToken);

            var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.TokenHash == hash, ct);

            if (pending is null)
            {
                return AuthResult.Fail("This confirmation link is no longer valid. Request a new one below.");
            }

            if (pending.ExpiresAt < DateTime.UtcNow)
            {
                // Kept on purpose so "send a fresh link" still has something to work with.
                return AuthResult.Fail("This confirmation link has expired. Request a new one below.");
            }

            // Google sign-in may have already created the account for this address.
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == pending.Email, ct);
            if (existing is not null)
            {
                _db.PendingRegistrations.Remove(pending);
                await _db.SaveChangesAsync(ct);
                return AuthResult.Ok(BuildAuthenticatedUser(existing), "Your account is already active.");
            }

            // The account is created only now, once the address is proven.
            var user = new AppUser
            {
                UserName = await GenerateUniqueUserNameAsync(pending.Email, ct),
                DisplayName = pending.DisplayName,
                Email = pending.Email,
                PasswordHash = pending.PasswordHash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.Users.Add(user);
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync(ct);

            return AuthResult.Ok(BuildAuthenticatedUser(user), "Your email is confirmed. Welcome aboard.");
        }

        public async Task<AuthResult> ResendVerificationAsync(string rawEmail, CancellationToken ct = default)
        {
            var email = rawEmail.Trim().ToLowerInvariant();
            var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == email, ct);

            if (pending is not null)
            {
                // Issuing a new link invalidates the previous one.
                var rawToken = NewToken();
                pending.TokenHash = HashToken(rawToken);
                pending.ExpiresAt = DateTime.UtcNow.Add(VerificationTokenLifetime);
                await _db.SaveChangesAsync(ct);

                await SendVerificationLinkAsync(email, pending.DisplayName, rawToken, ct);
            }

            // Always the same reply, so this cannot be used to discover registered addresses.
            return AuthResult.Notice("If that address needs confirming, a new link is on its way.");
        }

        private async Task SendVerificationLinkAsync(string email, string displayName, string rawToken, CancellationToken ct)
        {
            var appBaseUrl = _config["App:BaseUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("App:BaseUrl is not configured.");

            await _emailSender.SendVerificationEmailAsync(
                email,
                displayName,
                $"{appBaseUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}",
                ct);
        }

        private static string NewToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        private static string HashToken(string rawToken) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        public async Task<AppUser?> GetByIdAsync(int userId, CancellationToken ct = default) =>
            await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive, ct);

        private AuthenticatedUser BuildAuthenticatedUser(AppUser user)
        {
            var (token, expiresAt) = CreateToken(user);
            return new AuthenticatedUser
            {
                UserId = user.UserId,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Token = token,
                TokenExpiresAt = expiresAt
            };
        }

        private (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
        {
            var section = _config.GetSection("Jwt");
            var secret = section["Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");

            var expiresAt = DateTime.UtcNow.AddHours(
                double.TryParse(section["ExpiryHours"], out var hours) ? hours : 8);

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            };

            var token = new JwtSecurityToken(
                issuer: section["Issuer"],
                audience: section["Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
