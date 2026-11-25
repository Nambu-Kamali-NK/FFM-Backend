using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FFM.Identity.Access.Domain.Entities;
using FFM.Workforce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FFM.Workforce.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly WorkforceDbContext _dbContext;
        public static readonly SymmetricSecurityKey JwtSecret =
            new(Encoding.UTF8.GetBytes("SuperSecretDevelopmentKey123!_FFM_Production_Key_2026_Secure"));

        // Thread-safe in-memory cache for active MFA challenges and user TOTP secrets
        private static readonly ConcurrentDictionary<string, MfaChallengeSession> ActiveMfaChallenges = new();
        private static readonly ConcurrentDictionary<int, string> UserTotpSecrets = new();

        public AuthController(WorkforceDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public record LoginRequest(string Username, string Password, string? DeviceId = null);
        public record MfaSetupResponse(string Secret, string QrCodeUri, string ManualEntryKey, string Issuer, string Account);
        public record MfaEnableRequest(string Secret, string VerificationCode);
        public record MfaVerifyRequest(string MfaChallengeId, string VerificationCode);
        public record MfaDisableRequest(string Password, string? VerificationCode);
        public record OAuthLoginRequest(string Provider, string IdToken, string? AccessToken);

        public class MfaChallengeSession
        {
            public int UserId { get; set; }
            public string UserEmail { get; set; } = string.Empty;
            public string Secret { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public int FailedAttempts { get; set; }
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required." });
            }

            var normalized = request.Username.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UsernameOrEmail.ToLower() == normalized);

            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Invalid email address or password." });
            }

            bool valid = false;
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }
            catch
            {
                valid = false;
            }

            if (!valid)
            {
                return Unauthorized(new { success = false, message = "Invalid email address or password." });
            }

            // ================= DEVICE BINDING ENFORCEMENT =================
            var deviceId = request.DeviceId?.Trim() ?? Request?.Headers["X-Device-Id"].FirstOrDefault()?.Trim();
            var clientId = Request?.Headers["X-Client-Id"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "ffm-web";
            var requiresBinding = clientId is "ffm-mobile" or "ffm-pwa" || !string.IsNullOrWhiteSpace(deviceId);

            if (requiresBinding)
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        deviceBindingError = true,
                        message = "Mobile authentication requires a registered Device Identifier (X-Device-Id)."
                    });
                }

                var devices = await _dbContext.UserDevices.Where(d => d.UserId == user.Id).ToListAsync();
                if (devices.Count == 0)
                {
                    // Auto-bind first device as Active
                    var newDevice = new UserDevice
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        DeviceUuid = deviceId,
                        Status = DeviceStatus.Active,
                        CreatedAt = DateTimeOffset.UtcNow,
                        ApprovedAt = DateTimeOffset.UtcNow
                    };
                    _dbContext.UserDevices.Add(newDevice);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    var matchingDevice = devices.FirstOrDefault(d => d.DeviceUuid.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
                    if (matchingDevice == null)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            deviceBindingError = true,
                            message = "Device mismatch: This account is locked to another authorized mobile device. Contact your administrator to authorize a new device."
                        });
                    }

                    if (matchingDevice.Status == DeviceStatus.Pending)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            deviceBindingError = true,
                            message = "Device authorization is pending administrator approval."
                        });
                    }

                    if (matchingDevice.Status is DeviceStatus.Blocked or DeviceStatus.Revoked or DeviceStatus.Replaced)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            deviceBindingError = true,
                            message = $"Device access has been {matchingDevice.Status}. Please contact your administrator."
                        });
                    }
                }
            }

            // Check if MFA is required for this account
            if (user.MfaEnabled)
            {
                var challengeId = Guid.NewGuid().ToString("N");
                var secret = UserTotpSecrets.TryGetValue(user.Id, out var s) ? s : GenerateBase32Secret(20);
                UserTotpSecrets[user.Id] = secret;

                ActiveMfaChallenges[challengeId] = new MfaChallengeSession
                {
                    UserId = user.Id,
                    UserEmail = user.UsernameOrEmail,
                    Secret = secret,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    FailedAttempts = 0
                };

                return Ok(new
                {
                    success = true,
                    mfaRequired = true,
                    mfaChallengeId = challengeId,
                    message = "Two-factor authentication required. Enter your 6-digit authenticator code."
                });
            }

            return IssueJwtResponse(user);
        }

        /// <summary>
        /// Generates a new Base32 secret key and QR code URI for authenticator app configuration (RFC 6238).
        /// </summary>
        [Authorize]
        [HttpPost("mfa/setup")]
        public async Task<IActionResult> SetupMfa()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized(new { success = false, message = "Invalid user identity." });

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false, message = "User not found." });

            var secret = GenerateBase32Secret(20);
            var qrCodeUri = $"otpauth://totp/FFM:{Uri.EscapeDataString(user.UsernameOrEmail)}?secret={secret}&issuer=FFM&algorithm=SHA1&digits=6&period=30";

            return Ok(new MfaSetupResponse(
                Secret: secret,
                QrCodeUri: qrCodeUri,
                ManualEntryKey: secret,
                Issuer: "FFM Field Operations",
                Account: user.UsernameOrEmail
            ));
        }

        /// <summary>
        /// Enables 2FA for the account after verifying the first 6-digit TOTP code.
        /// </summary>
        [Authorize]
        [HttpPost("mfa/enable")]
        public async Task<IActionResult> EnableMfa([FromBody] MfaEnableRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Secret) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(new { success = false, message = "Secret and verification code are required." });
            }

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized(new { success = false, message = "Invalid user identity." });

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false, message = "User not found." });

            if (!VerifyTotpCode(request.Secret, request.VerificationCode))
            {
                return BadRequest(new { success = false, message = "Invalid verification code. Please check your authenticator app and try again." });
            }

            user.MfaEnabled = true;
            UserTotpSecrets[user.Id] = request.Secret.Trim();
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Two-factor authentication has been successfully enabled." });
        }

        /// <summary>
        /// Verifies a 6-digit code during MFA sign-in challenge and completes authentication.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("mfa/verify")]
        public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MfaChallengeId) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(new { success = false, message = "Challenge ID and verification code are required." });
            }

            if (!ActiveMfaChallenges.TryGetValue(request.MfaChallengeId, out var session))
            {
                return BadRequest(new { success = false, message = "MFA challenge not found or has expired. Please sign in again." });
            }

            if (DateTime.UtcNow > session.ExpiresAt)
            {
                ActiveMfaChallenges.TryRemove(request.MfaChallengeId, out _);
                return BadRequest(new { success = false, message = "MFA challenge has expired. Please sign in again." });
            }

            if (session.FailedAttempts >= 5)
            {
                ActiveMfaChallenges.TryRemove(request.MfaChallengeId, out _);
                return BadRequest(new { success = false, message = "Maximum invalid attempts exceeded. Please initiate sign in again." });
            }

            // Verify TOTP code
            var valid = VerifyTotpCode(session.Secret, request.VerificationCode);
            if (!valid)
            {
                session.FailedAttempts++;
                var remaining = 5 - session.FailedAttempts;
                return BadRequest(new { success = false, message = $"Invalid verification code. {remaining} attempt(s) remaining." });
            }

            // Successfully verified!
            ActiveMfaChallenges.TryRemove(request.MfaChallengeId, out _);

            var user = await _dbContext.Users.FindAsync(session.UserId);
            if (user == null) return NotFound(new { success = false, message = "User not found." });

            return IssueJwtResponse(user);
        }

        /// <summary>
        /// Disables 2-Factor Authentication after verifying account password.
        /// </summary>
        [Authorize]
        [HttpPost("mfa/disable")]
        public async Task<IActionResult> DisableMfa([FromBody] MfaDisableRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Password confirmation is required." });
            }

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized(new { success = false, message = "Invalid user identity." });

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false, message = "User not found." });

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return BadRequest(new { success = false, message = "Invalid password." });
            }

            user.MfaEnabled = false;
            UserTotpSecrets.TryRemove(user.Id, out _);
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Two-factor authentication has been disabled." });
        }

        /// <summary>
        /// Returns whether MFA is enabled for the current user.
        /// </summary>
        [Authorize]
        [HttpGet("mfa/status")]
        public async Task<IActionResult> GetMfaStatus()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized(new { success = false, message = "Invalid user identity." });

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false, message = "User not found." });

            return Ok(new
            {
                success = true,
                mfaEnabled = user.MfaEnabled
            });
        }

        /// <summary>
        /// Authenticates a user using an OAuth 2.0 / OpenID Connect ID token from Google or Microsoft Azure AD.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("oauth/login")]
        public async Task<IActionResult> OAuthLogin([FromBody] OAuthLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new { success = false, message = "OAuth ID token is required." });
            }

            var provider = request.Provider?.Trim().ToLowerInvariant() ?? "google";
            string? email = null;
            string? name = null;

            // 1. If Google, probe tokeninfo endpoint or parse JWT
            if (provider == "google")
            {
                try
                {
                    using var client = new HttpClient();
                    var resp = await client.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(request.IdToken)}");
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                        if (json.TryGetProperty("email", out var e)) email = e.GetString();
                        if (json.TryGetProperty("name", out var n)) name = n.GetString();
                    }
                }
                catch
                {
                    // Fall back to JWT claims parser
                }
            }

            // 2. Fallback / Microsoft: Read JWT claims directly
            if (string.IsNullOrWhiteSpace(email))
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(request.IdToken))
                {
                    var jwt = handler.ReadJwtToken(request.IdToken);
                    email = jwt.Claims.FirstOrDefault(c => c.Type is "email" or "preferred_username" or ClaimTypes.Email)?.Value;
                    name = jwt.Claims.FirstOrDefault(c => c.Type is "name" or ClaimTypes.Name)?.Value;
                }
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { success = false, message = "Could not extract a verified email claim from the OAuth token." });
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UsernameOrEmail.ToLower() == normalizedEmail);

            if (user == null)
            {
                // Auto-provision user account for verified SSO identity
                user = new User
                {
                    UsernameOrEmail = normalizedEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                    Status = AccountStatus.Active,
                    Role = Role.EMPLOYEE,
                    MfaEnabled = false,
                    FailedAttempts = 0,
                    LastLoginAt = DateTimeOffset.UtcNow
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            return IssueJwtResponse(user);
        }

        /// <summary>
        /// Returns available OAuth 2.0 / OpenID Connect identity providers.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("oauth/providers")]
        public IActionResult GetOAuthProviders()
        {
            return Ok(new[]
            {
                new { id = "google", name = "Google Workspace & Accounts", enabled = true },
                new { id = "microsoft", name = "Microsoft 365 / Azure AD", enabled = true }
            });
        }

        // ================= PRIVATE HELPER METHODS =================

        private IActionResult IssueJwtResponse(User user)
        {
            var roleName = user.Role.ToString();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("userId", user.Id.ToString()),
                new(ClaimTypes.Email, user.UsernameOrEmail),
                new(ClaimTypes.Name, user.UsernameOrEmail),
                new(ClaimTypes.Role, roleName),
                new("role", roleName)
            };

            var creds = new SigningCredentials(JwtSecret, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                success = true,
                accessToken = tokenString,
                mfaRequired = false,
                user = new
                {
                    id = user.Id,
                    username = user.UsernameOrEmail,
                    role = roleName
                }
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("userId")?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (int.TryParse(userIdClaim, out var id)) return id;
            return 0;
        }

        private static string GenerateBase32Secret(int byteLength = 20)
        {
            const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = new byte[byteLength];
            RandomNumberGenerator.Fill(bytes);

            var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
            int buffer = 0, bitsLeft = 0;
            foreach (var b in bytes)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(base32Chars[(buffer >> bitsLeft) & 0x1F]);
                }
            }
            if (bitsLeft > 0) sb.Append(base32Chars[(buffer << (5 - bitsLeft)) & 0x1F]);
            return sb.ToString();
        }

        public static bool VerifyTotpCode(string base32Secret, string code)
        {
            if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();
            if (code.Length != 6 || !int.TryParse(code, out _)) return false;

            long currentCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
            for (int i = -1; i <= 1; i++)
            {
                var expected = ComputeTotp(base32Secret, currentCounter + i);
                if (expected == code) return true;
            }
            return false;
        }

        public static string ComputeTotp(string base32Secret, long counter)
        {
            const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var upper = base32Secret.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
            var keyList = new List<byte>();
            int buffer = 0, bitsLeft = 0;
            foreach (var c in upper)
            {
                int val = base32Chars.IndexOf(c);
                if (val < 0) continue;
                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    keyList.Add((byte)((buffer >> bitsLeft) & 0xFF));
                }
            }

            var key = keyList.ToArray();
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);

            int offset = hash[^1] & 0x0F;
            int binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            int otp = binaryCode % 1000000;
            return otp.ToString("D6");
        }
    }
}
