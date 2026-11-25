// File: AuthService.cs
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Models;
using FFM.Identity.Access.Domain.Options;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FFM.Identity.Access.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly IdentityDbContext _dbContext;
        private readonly JwtOptions _jwtOptions;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IEmailSender? _emailSender;
        private readonly ISmsSender? _smsSender;

        public AuthService(
            IdentityDbContext dbContext,
            IOptions<JwtOptions> jwtOptions,
            IRefreshTokenService refreshTokenService,
            IEmailSender? emailSender = null,
            ISmsSender? smsSender = null)
        {
            _dbContext = dbContext;
            _jwtOptions = jwtOptions.Value;
            _refreshTokenService = refreshTokenService;
            _emailSender = emailSender;
            _smsSender = smsSender;
        }

        private const string DummyHash = "$2a$11$KUlNqk79GTWm33D/JBI2Re59uGyHGnfsYbq0Mo3.uuJbJXIW0bwKy";

        private static RsaSecurityKey? _cachedSigningKey;
        private static readonly object _keyLock = new();

        private RsaSecurityKey GetSigningKey()
        {
            if (_cachedSigningKey != null) return _cachedSigningKey;

            lock (_keyLock)
            {
                if (_cachedSigningKey != null) return _cachedSigningKey;

                var rsa = RSA.Create();
                var keyBytes = Convert.FromBase64String(_jwtOptions.RsaPrivateKey);
                rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                _cachedSigningKey = new RsaSecurityKey(rsa)
                {
                    KeyId = "ffm-rsa-key-1"
                };
                return _cachedSigningKey;
            }
        }

        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request, string? clientId = null, string? ipAddress = null)
        {
            var effectiveClientId = !string.IsNullOrWhiteSpace(clientId) ? clientId : "ffm-mobile";

            if (request == null)
                return null!;

            // Find user by identifier (username or email)
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UsernameOrEmail == request.Username);

            if (user == null)
            {
                // Mitigate timing attacks / username enumeration by performing a constant-time dummy BCrypt verification
                BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, DummyHash);
                return null!; // Controller will treat as unauthorized
            }

            // Check account lockout
            if (user.Status == AccountStatus.Locked)
            {
                // If locked indefinitely (no expiration set) or lockout duration is still in effect
                if (!user.LockedUntil.HasValue || user.LockedUntil.Value > DateTimeOffset.UtcNow)
                {
                    return null!; // Account remains locked
                }

                // Temporary lockout duration expired - restore to Active
                user.Status = AccountStatus.Active;
                user.FailedAttempts = 0;
                user.LockedUntil = null;
                await _dbContext.SaveChangesAsync();
            }

            // Only Active accounts are allowed to authenticate
            if (user.Status != AccountStatus.Active)
            {
                return null!; // Inactive, PendingActivation, Suspended, Blocked, or Deactivated
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                // Increment failed attempts (simplified)
                user.FailedAttempts++;
                if (user.FailedAttempts >= 5)
                {
                    user.Status = AccountStatus.Locked;
                    user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);
                }
                await _dbContext.SaveChangesAsync();
                return null!;
            }

            // Reset failed attempts on success
            user.FailedAttempts = 0;
            user.LastLoginAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            // ================= 2-FACTOR AUTHENTICATION CHECK =================
            if (user.MfaEnabled)
            {
                // Invalidate any existing active challenges for this user
                var existingChallenges = await _dbContext.MfaChallenges
                    .Where(c => c.UserId == user.Id && !c.Verified && c.ExpiresAt > DateTimeOffset.UtcNow)
                    .ToListAsync();

                foreach (var ec in existingChallenges)
                {
                    ec.ExpiresAt = DateTimeOffset.UtcNow; // expire immediately
                }

                // Generate 6-digit secure numeric OTP (100000 - 999999)
                var otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
                var rawOtp = otpNumber.ToString("D6");
                var otpHash = TokenHasher.ComputeHash(rawOtp);

                var challenge = new MfaChallenge
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    OtpHash = otpHash,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    FailedAttempts = 0,
                    Verified = false
                };

                _dbContext.MfaChallenges.Add(challenge);

                _dbContext.AuthAuditLogs.Add(new AuthAuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    EventType = AuditEventType.MfaChallengeCreated,
                    Timestamp = DateTimeOffset.UtcNow,
                    IpAddress = ipAddress ?? "unknown",
                    AdditionalInfo = $"ChallengeId: {challenge.Id}"
                });

                await _dbContext.SaveChangesAsync();

                // Dispatch OTP in real time via Email and SMS providers
                if (_emailSender != null)
                {
                    await _emailSender.SendMfaCodeEmailAsync(user.UsernameOrEmail, rawOtp);
                }

                if (_smsSender != null)
                {
                    await _smsSender.SendMfaCodeSmsAsync(user.UsernameOrEmail, rawOtp);
                }

                return new LoginResponse
                {
                    AccessToken = string.Empty,
                    RefreshToken = string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    MfaRequired = true,
                    MfaChallengeId = challenge.Id.ToString()
                };
            }

            // If MFA is not required, immediately issue full JWT and refresh tokens
            return await IssueTokensAsync(user, effectiveClientId, ipAddress);
        }

        public async Task<(bool Success, LoginResponse? Response, string Message)> VerifyMfaAsync(
            MfaVerifyRequest request, string? clientId = null, string? ipAddress = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MfaChallengeId) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return (false, null, "Challenge ID and verification code are required.");
            }

            if (!Guid.TryParse(request.MfaChallengeId, out var challengeId))
            {
                return (false, null, "Invalid MFA challenge ID format.");
            }

            var challenge = await _dbContext.MfaChallenges
                .FirstOrDefaultAsync(c => c.Id == challengeId);

            if (challenge == null)
            {
                return (false, null, "MFA challenge not found.");
            }

            if (challenge.Verified)
            {
                return (false, null, "This verification challenge has already been completed.");
            }

            if (challenge.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return (false, null, "Verification code has expired. Please request a new code.");
            }

            if (challenge.FailedAttempts >= 5)
            {
                return (false, null, "Maximum invalid attempts exceeded. Please initiate sign-in again.");
            }

            // Verify OTP code hash
            var providedCodeHash = TokenHasher.ComputeHash(request.VerificationCode.Trim());
            if (providedCodeHash != challenge.OtpHash)
            {
                challenge.FailedAttempts++;
                await _dbContext.SaveChangesAsync();
                var remaining = 5 - challenge.FailedAttempts;
                return (false, null, $"Invalid verification code. {remaining} attempt(s) remaining.");
            }

            // Code verified successfully!
            challenge.Verified = true;

            var user = await _dbContext.Users.FindAsync(challenge.UserId);
            if (user == null || user.Status != AccountStatus.Active)
            {
                return (false, null, "User account is invalid or no longer active.");
            }

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.MfaChallengeVerified,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = ipAddress ?? "unknown",
                AdditionalInfo = $"ChallengeId: {challenge.Id}"
            });

            await _dbContext.SaveChangesAsync();

            var effectiveClientId = !string.IsNullOrWhiteSpace(clientId) ? clientId : "ffm-web";
            var tokens = await IssueTokensAsync(user, effectiveClientId, ipAddress);

            return (true, tokens, "Two-factor verification successful.");
        }

        public async Task<(bool Success, string Message)> ResendMfaCodeAsync(string mfaChallengeId, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(mfaChallengeId) || !Guid.TryParse(mfaChallengeId, out var challengeId))
            {
                return (false, "Invalid MFA challenge ID.");
            }

            var challenge = await _dbContext.MfaChallenges.FirstOrDefaultAsync(c => c.Id == challengeId);
            if (challenge == null || challenge.Verified)
            {
                return (false, "Active challenge not found.");
            }

            var user = await _dbContext.Users.FindAsync(challenge.UserId);
            if (user == null || user.Status != AccountStatus.Active)
            {
                return (false, "User account not active.");
            }

            // Invalidate old challenge
            challenge.ExpiresAt = DateTimeOffset.UtcNow;

            // Generate new OTP
            var otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
            var rawOtp = otpNumber.ToString("D6");
            var otpHash = TokenHasher.ComputeHash(rawOtp);

            var newChallenge = new MfaChallenge
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpHash = otpHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                FailedAttempts = 0,
                Verified = false
            };

            _dbContext.MfaChallenges.Add(newChallenge);
            await _dbContext.SaveChangesAsync();

            if (_emailSender != null)
            {
                await _emailSender.SendMfaCodeEmailAsync(user.UsernameOrEmail, rawOtp);
            }

            if (_smsSender != null)
            {
                await _smsSender.SendMfaCodeSmsAsync(user.UsernameOrEmail, rawOtp);
            }

            return (true, "New verification code has been dispatched.");
        }

        public async Task<MfaSetupResponse> SetupMfaAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found.");
            }

            var secret = TotpService.GenerateSecret(20);
            var qrCodeUri = TotpService.GetQrCodeUri("FFM", user.UsernameOrEmail, secret);

            // Create temporary setup challenge in database
            var otpHash = TokenHasher.ComputeHash(secret);
            var challenge = new MfaChallenge
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpHash = otpHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                FailedAttempts = 0,
                Verified = false
            };
            _dbContext.MfaChallenges.Add(challenge);
            await _dbContext.SaveChangesAsync();

            return new MfaSetupResponse
            {
                Secret = secret,
                QrCodeUri = qrCodeUri,
                ManualEntryKey = secret,
                Issuer = "FFM Field Operations",
                Account = user.UsernameOrEmail
            };
        }

        public async Task<(bool Success, string Message)> EnableMfaAsync(int userId, MfaEnableRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Secret) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return (false, "Secret and verification code are required.");
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Verify TOTP code against the secret
            var isValid = TotpService.VerifyTotpCode(request.Secret, request.VerificationCode);
            if (!isValid)
            {
                return (false, "Invalid verification code. Please check your authenticator app and try again.");
            }

            user.MfaEnabled = true;

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.MfaChallengeVerified,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = "internal",
                AdditionalInfo = "MFA enabled via Authenticator TOTP"
            });

            await _dbContext.SaveChangesAsync();
            return (true, "Two-factor authentication has been successfully enabled.");
        }

        public async Task<(bool Success, string Message)> DisableMfaAsync(int userId, MfaDisableRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Password))
            {
                return (false, "Current account password is required to disable two-factor authentication.");
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Verify password for security
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return (false, "Invalid password confirmation.");
            }

            user.MfaEnabled = false;

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.PasswordResetCompleted,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = "internal",
                AdditionalInfo = "MFA disabled"
            });

            await _dbContext.SaveChangesAsync();
            return (true, "Two-factor authentication has been disabled.");
        }

        public async Task<LoginResponse> IssueTokensAsync(User user, string effectiveClientId, string? ipAddress = null)
        {
            // Create JWT access token
            var tokenHandler = new JwtSecurityTokenHandler();
            var signingKey = GetSigningKey();

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("userId", user.Id.ToString()),
                new Claim("user_id", user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("client_id", effectiveClientId),
                new Claim("role", user.Role.ToString())
            };
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtOptions.Issuer,
                Audience = _jwtOptions.Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
            };

            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(securityToken);

            // Generate secure refresh token and persist hashed version
            var rawRefreshToken = Guid.NewGuid().ToString("N");
            var tokenHash = TokenHasher.ComputeHash(rawRefreshToken);
            var now = DateTimeOffset.UtcNow;
            var refreshEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ClientId = effectiveClientId,
                IssuedAt = now,
                ExpiresAt = now.AddDays(_jwtOptions.RefreshExpiryDays),
                RevokedAt = null,
                ReplacedById = null
            };
            _dbContext.RefreshTokens.Add(refreshEntity);

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.LoginSuccess,
                Timestamp = now,
                IpAddress = ipAddress ?? "unknown"
            });

            await _dbContext.SaveChangesAsync();

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken,
                ExpiresAt = tokenDescriptor.Expires ?? DateTime.UtcNow,
                MfaRequired = false,
                MfaChallengeId = null
            };
        }

        public async Task<bool> ForgotPasswordAsync(string email, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return true;

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UsernameOrEmail.ToLower() == normalizedEmail);

            // Anti-User Enumeration: Always return true even if user does not exist
            if (user == null)
            {
                return true;
            }

            // Invalidate any existing unused reset tokens for this user
            var existingTokens = await _dbContext.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTimeOffset.UtcNow)
                .ToListAsync();

            foreach (var t in existingTokens)
            {
                t.IsUsed = true;
                t.UsedAt = DateTimeOffset.UtcNow;
            }

            // Generate cryptographically secure token (32 bytes = 64 hex characters)
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();
            var tokenHash = TokenHasher.ComputeHash(rawToken);

            var resetTokenEntity = new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTimeOffset.UtcNow,
                IpAddress = ipAddress
            };

            _dbContext.PasswordResetTokens.Add(resetTokenEntity);

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.PasswordResetRequested,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = ipAddress ?? "unknown"
            });

            await _dbContext.SaveChangesAsync();

            // Dispatch notification / email
            if (_emailSender != null)
            {
                await _emailSender.SendPasswordResetEmailAsync(user.UsernameOrEmail, rawToken);
            }

            return true;
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "Reset token cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return (false, "Password must be at least 8 characters long.");
            }

            var tokenHash = TokenHasher.ComputeHash(token.Trim());

            var resetToken = await _dbContext.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (resetToken == null || resetToken.IsUsed || resetToken.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return (false, "The password reset token is invalid, expired, or has already been used.");
            }

            var user = resetToken.User ?? await _dbContext.Users.FindAsync(resetToken.UserId);
            if (user == null)
            {
                return (false, "User account associated with this token was not found.");
            }

            // Update password hash using BCrypt
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.FailedAttempts = 0;
            user.LockedUntil = null;

            // Mark token as consumed
            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTimeOffset.UtcNow;

            // Revoke all active refresh tokens for this user across all devices
            var activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var rt in activeRefreshTokens)
            {
                rt.RevokedAt = DateTimeOffset.UtcNow;
            }

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.PasswordResetCompleted,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = ipAddress ?? "unknown"
            });

            await _dbContext.SaveChangesAsync();

            return (true, "Password has been reset successfully. Please log in with your new credentials.");
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string? ipAddress = null)
        {
            if (userId <= 0)
            {
                return (false, "Invalid user identifier.");
            }

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                return (false, "Current password cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return (false, "New password must be at least 8 characters long.");
            }

            if (string.Equals(currentPassword, newPassword))
            {
                return (false, "New password cannot be identical to the current password.");
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null || user.Status != AccountStatus.Active)
            {
                return (false, "User account not found or is inactive.");
            }

            // Verify current password using BCrypt
            var isCurrentValid = BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash);
            if (!isCurrentValid)
            {
                return (false, "Current password is incorrect.");
            }

            // Hash new password using BCrypt
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.FailedAttempts = 0;
            user.LockedUntil = null;

            // Revoke active refresh tokens for this user for security
            var activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var rt in activeRefreshTokens)
            {
                rt.RevokedAt = DateTimeOffset.UtcNow;
            }

            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.PasswordResetCompleted,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = ipAddress ?? "unknown"
            });

            await _dbContext.SaveChangesAsync();

            return (true, "Password changed successfully.");
        }
    }
}
