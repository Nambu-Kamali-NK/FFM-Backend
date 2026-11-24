// File: RefreshTokenService.cs
using FFM.Identity.Access.Domain.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FFM.Identity.Access.Api.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IdentityDbContext _dbContext;
        private readonly JwtOptions _jwtOptions;

        public RefreshTokenService(IdentityDbContext dbContext, IOptions<JwtOptions> jwtOptions)
        {
            _dbContext = dbContext;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<LoginResponse?> RefreshAsync(RefreshRequest request)
        {
            // Compute hash of provided token
            var tokenHash = TokenHasher.ComputeHash(request.RefreshToken);

            var now = DateTimeOffset.UtcNow;
            var existingToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null && rt.ExpiresAt > now);

            if (existingToken == null)
            {
                // Check if a revoked token is being replayed (breach detection)
                var revokedToken = await _dbContext.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt != null);

                if (revokedToken != null)
                {
                    // Token reuse detected: revoke all active tokens for this user
                    await RevokeUserTokensAsync(revokedToken.UserId);
                }

                return null;
            }

            var effectiveClientId = existingToken.ClientId ?? "ffm-mobile";

            // Load user
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == existingToken.UserId);
            if (user == null || user.Status != AccountStatus.Active)
                return null;

            // Generate new access token (similar to AuthService)
            var tokenHandler = new JwtSecurityTokenHandler();
            var rsa = RSA.Create();
            var keyBytes = Convert.FromBase64String(_jwtOptions.RsaPrivateKey);
            rsa.ImportPkcs8PrivateKey(keyBytes, out _);
            var signingKey = new RsaSecurityKey(rsa) { KeyId = "ffm-rsa-key-1" };

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

            // Create new refresh token and revoke old one
            var newRefreshTokenRaw = Guid.NewGuid().ToString("N");
            var newRefreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = TokenHasher.ComputeHash(newRefreshTokenRaw),
                ClientId = effectiveClientId, // Preserve client ID
                IssuedAt = now,
                ExpiresAt = now.AddDays(_jwtOptions.RefreshExpiryDays),
                RevokedAt = null,
                ReplacedById = null
            };

            existingToken.RevokedAt = now;
            existingToken.ReplacedById = newRefreshToken.Id;

            _dbContext.RefreshTokens.Add(newRefreshToken);
            await _dbContext.SaveChangesAsync();

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshTokenRaw,
                ExpiresAt = tokenDescriptor.Expires ?? DateTime.UtcNow,
                MfaRequired = false,
                MfaChallengeId = null
            };
        }

        public async Task RevokeUserTokensAsync(int userId)
        {
            var tokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            foreach (var token in tokens)
            {
                token.RevokedAt = now;
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}