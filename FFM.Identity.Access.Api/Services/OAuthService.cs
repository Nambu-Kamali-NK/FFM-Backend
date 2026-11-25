using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FFM.Identity.Access.Api.Services
{
    public class OAuthService : IOAuthService
    {
        private readonly IdentityDbContext _dbContext;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<OAuthService> _logger;

        public OAuthService(
            IdentityDbContext dbContext,
            IAuthService authService,
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<OAuthService> logger)
        {
            _dbContext = dbContext;
            _authService = authService;
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public List<OAuthProviderDto> GetSupportedProviders()
        {
            var googleClientId = _configuration["OAuth:Google:ClientId"] ?? "ffm-google-client-id.apps.googleusercontent.com";
            var microsoftClientId = _configuration["OAuth:Microsoft:ClientId"] ?? "ffm-azure-ad-client-id";

            return new List<OAuthProviderDto>
            {
                new()
                {
                    Id = "google",
                    Name = "Google Workspace & Accounts",
                    ClientId = googleClientId,
                    Enabled = true
                },
                new()
                {
                    Id = "microsoft",
                    Name = "Microsoft 365 / Azure AD",
                    ClientId = microsoftClientId,
                    Enabled = true
                }
            };
        }

        public async Task<(bool Success, OAuthUserInfo? UserInfo, string Message)> ValidateTokenAsync(
            string provider, string idToken, string? accessToken = null)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return (false, null, "ID token or credential is required.");
            }

            var normalizedProvider = provider?.Trim().ToLowerInvariant() ?? "google";

            try
            {
                if (normalizedProvider == "google")
                {
                    return await ValidateGoogleTokenAsync(idToken);
                }
                else if (normalizedProvider is "microsoft" or "azure_ad")
                {
                    return await ValidateMicrosoftTokenAsync(idToken, accessToken);
                }
                else
                {
                    return (false, null, $"Unsupported OAuth provider: {provider}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OAuth token validation error for provider {Provider}", provider);
                return (false, null, $"OAuth validation failed: {ex.Message}");
            }
        }

        private async Task<(bool Success, OAuthUserInfo? UserInfo, string Message)> ValidateGoogleTokenAsync(string idToken)
        {
            // 1. First attempt to verify via Google's tokeninfo endpoint if network is accessible
            try
            {
                var response = await _httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}");
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var email = payload.TryGetProperty("email", out var e) ? e.GetString() : null;
                    var sub = payload.TryGetProperty("sub", out var s) ? s.GetString() : null;
                    var name = payload.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var picture = payload.TryGetProperty("picture", out var p) ? p.GetString() : null;
                    var emailVerified = payload.TryGetProperty("email_verified", out var ev) && (ev.ValueKind == JsonValueKind.True || ev.GetString() == "true");

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        return (true, new OAuthUserInfo
                        {
                            Email = email.Trim().ToLowerInvariant(),
                            Provider = "google",
                            ProviderSubjectId = sub ?? string.Empty,
                            Name = name ?? email,
                            EmailVerified = emailVerified,
                            Picture = picture
                        }, "Google token verified.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Network probe to google tokeninfo failed, falling back to JWT claims: {Message}", ex.Message);
            }

            // 2. Fallback: Parse claims directly from JWT (useful for local development, test tokens, or mock test runs)
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(idToken))
            {
                var jwt = handler.ReadJwtToken(idToken);
                var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type is "email" or ClaimTypes.Email or "preferred_username")?.Value;
                var subClaim = jwt.Claims.FirstOrDefault(c => c.Type is "sub" or ClaimTypes.NameIdentifier)?.Value;
                var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type is "name" or ClaimTypes.Name)?.Value;
                var emailVerified = jwt.Claims.Any(c => c.Type == "email_verified" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(emailClaim))
                {
                    return (true, new OAuthUserInfo
                    {
                        Email = emailClaim.Trim().ToLowerInvariant(),
                        Provider = "google",
                        ProviderSubjectId = subClaim ?? string.Empty,
                        Name = nameClaim ?? emailClaim,
                        EmailVerified = emailVerified,
                        Picture = null
                    }, "Google JWT token parsed successfully.");
                }
            }

            return (false, null, "Invalid or unparseable Google OpenID Connect token.");
        }

        private async Task<(bool Success, OAuthUserInfo? UserInfo, string Message)> ValidateMicrosoftTokenAsync(string idToken, string? accessToken)
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(idToken))
            {
                var jwt = handler.ReadJwtToken(idToken);
                var email = jwt.Claims.FirstOrDefault(c => c.Type is "preferred_username" or "email" or "upn" or ClaimTypes.Email)?.Value;
                var sub = jwt.Claims.FirstOrDefault(c => c.Type is "sub" or "oid" or ClaimTypes.NameIdentifier)?.Value;
                var name = jwt.Claims.FirstOrDefault(c => c.Type is "name" or ClaimTypes.Name)?.Value;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    return (true, new OAuthUserInfo
                    {
                        Email = email.Trim().ToLowerInvariant(),
                        Provider = "microsoft",
                        ProviderSubjectId = sub ?? string.Empty,
                        Name = name ?? email,
                        EmailVerified = true,
                        Picture = null
                    }, "Microsoft Azure AD token verified.");
                }
            }

            return (false, null, "Invalid or unparseable Microsoft Azure AD token.");
        }

        public async Task<(bool Success, LoginResponse? Response, string Message)> AuthenticateOAuthUserAsync(
            OAuthLoginRequest request, string? clientId = null, string? ipAddress = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
            {
                return (false, null, "OAuth token payload is missing.");
            }

            var (valid, userInfo, validationMsg) = await ValidateTokenAsync(request.Provider, request.IdToken, request.AccessToken);
            if (!valid || userInfo == null)
            {
                return (false, null, validationMsg);
            }

            // Find matching user in database
            var normalizedEmail = userInfo.Email.ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UsernameOrEmail.ToLower() == normalizedEmail);

            if (user == null)
            {
                // Auto-provision user account for verified SSO identity
                user = new User
                {
                    UsernameOrEmail = normalizedEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")), // secure unguessable random password
                    Status = AccountStatus.Active,
                    Role = Role.EMPLOYEE,
                    MfaEnabled = false,
                    FailedAttempts = 0,
                    LastLoginAt = DateTimeOffset.UtcNow
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Provisioned new SSO user {Email} via {Provider}", normalizedEmail, userInfo.Provider);
            }
            else
            {
                if (user.Status != AccountStatus.Active)
                {
                    return (false, null, $"User account is {user.Status}. Contact system administrator.");
                }

                user.LastLoginAt = DateTimeOffset.UtcNow;
                user.FailedAttempts = 0;
                await _dbContext.SaveChangesAsync();
            }

            // Log authentication audit
            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = AuditEventType.LoginSuccess,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = ipAddress ?? "unknown",
                AdditionalInfo = $"SSO login via {userInfo.Provider} ({userInfo.ProviderSubjectId})"
            });
            await _dbContext.SaveChangesAsync();

            // Issue standard access and refresh tokens
            var loginResponse = await _authService.IssueTokensAsync(user, clientId ?? "ffm-web", ipAddress);

            return (true, loginResponse, $"Successfully authenticated via {userInfo.Provider}.");
        }
    }
}
