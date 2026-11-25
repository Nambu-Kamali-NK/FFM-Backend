using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Entities;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class IdentityMfaOAuthTests
    {
        private static System.Text.Json.JsonElement ToJson(object? obj)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            return System.Text.Json.JsonDocument.Parse(json).RootElement;
        }

        private WorkforceDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<WorkforceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new WorkforceDbContext(options);
        }

        // ================= TOTP RFC 6238 UNIT TESTS =================

        [Fact]
        public void TotpService_GenerateSecret_ReturnsValidBase32Secret()
        {
            var secret = TotpService.GenerateSecret(20);

            Assert.NotNull(secret);
            Assert.NotEmpty(secret);
            Assert.True(secret.Length >= 26); // 20 bytes in Base32 = 32 chars

            // Verify characters are Base32 (A-Z, 2-7)
            foreach (var c in secret)
            {
                Assert.True(char.IsLetter(c) || (c >= '2' && c <= '7'));
            }
        }

        [Fact]
        public void TotpService_GenerateAndVerify_ReturnsTrue()
        {
            var secret = TotpService.GenerateSecret(20);
            var code = TotpService.GenerateTotpCode(secret);

            Assert.NotNull(code);
            Assert.Equal(6, code.Length);

            var isValid = TotpService.VerifyTotpCode(secret, code);
            Assert.True(isValid);
        }

        [Fact]
        public void TotpService_InvalidCode_ReturnsFalse()
        {
            var secret = TotpService.GenerateSecret(20);
            var isValid = TotpService.VerifyTotpCode(secret, "000000");

            // Unless 000000 happens to be the exact OTP for this moment, it should fail
            var currentOtp = TotpService.GenerateTotpCode(secret);
            if (currentOtp != "000000")
            {
                Assert.False(isValid);
            }
        }

        [Fact]
        public void TotpService_GetQrCodeUri_ConformsToOtpAuthFormat()
        {
            var secret = "JBSWY3DPEHPK3PXP";
            var uri = TotpService.GetQrCodeUri("FFM Platform", "technician@ffm.om", secret);

            Assert.StartsWith("otpauth://totp/", uri);
            Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
            Assert.Contains("issuer=FFM", uri);
            Assert.Contains("digits=6", uri);
        }

        // ================= AUTH CONTROLLER MFA & OAUTH INTEGRATION TESTS =================

        [Fact]
        public async Task AuthController_Login_WithMfaEnabledUser_ReturnsMfaChallengeRequired()
        {
            var db = GetInMemoryDbContext();
            var password = "SecurePassword123!";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new User
            {
                UsernameOrEmail = "nambu.mfa@ffm.om",
                PasswordHash = passwordHash,
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE,
                MfaEnabled = true // MFA required
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var controller = new AuthController(db);
            var loginResult = await controller.Login(new AuthController.LoginRequest("nambu.mfa@ffm.om", password));

            var okResult = Assert.IsType<OkObjectResult>(loginResult);
            var data = ToJson(okResult.Value);

            Assert.True(data.GetProperty("mfaRequired").GetBoolean());
            Assert.NotNull(data.GetProperty("mfaChallengeId").GetString());
            Assert.Contains("Two-factor authentication required", data.GetProperty("message").GetString());
        }

        [Fact]
        public async Task AuthController_MfaLifecycle_SetupEnableVerify_Succeeds()
        {
            var db = GetInMemoryDbContext();
            var password = "SecurePassword123!";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new User
            {
                UsernameOrEmail = "technician.audit@ffm.om",
                PasswordHash = passwordHash,
                Status = AccountStatus.Active,
                Role = Role.EMPLOYEE,
                MfaEnabled = false
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var controller = new AuthController(db);

            // Mock authenticated user context
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("userId", user.Id.ToString())
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            // 1. Setup MFA
            var setupResult = await controller.SetupMfa();
            var okSetup = Assert.IsType<OkObjectResult>(setupResult);
            var setupData = Assert.IsType<AuthController.MfaSetupResponse>(okSetup.Value);

            Assert.NotEmpty(setupData.Secret);
            Assert.StartsWith("otpauth://totp/", setupData.QrCodeUri);

            // 2. Generate valid TOTP from returned secret and Enable MFA
            var validCode = AuthController.ComputeTotp(setupData.Secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
            var enableResult = await controller.EnableMfa(new AuthController.MfaEnableRequest(setupData.Secret, validCode));
            var okEnable = Assert.IsType<OkObjectResult>(enableResult);
            var enableData = ToJson(okEnable.Value);
            Assert.True(enableData.GetProperty("success").GetBoolean());

            // Verify user has MfaEnabled == true in DB
            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.True(updatedUser!.MfaEnabled);

            // 3. User logs in -> gets MFA Challenge
            var loginResult = await controller.Login(new AuthController.LoginRequest("technician.audit@ffm.om", password));
            var okLogin = Assert.IsType<OkObjectResult>(loginResult);
            var loginData = ToJson(okLogin.Value);
            Assert.True(loginData.GetProperty("mfaRequired").GetBoolean());
            string challengeId = loginData.GetProperty("mfaChallengeId").GetString()!;

            // 4. Verify Challenge using TOTP code
            var codeToVerify = AuthController.ComputeTotp(setupData.Secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
            var verifyResult = await controller.VerifyMfa(new AuthController.MfaVerifyRequest(challengeId, codeToVerify));
            var okVerify = Assert.IsType<OkObjectResult>(verifyResult);
            var verifyData = ToJson(okVerify.Value);

            Assert.True(verifyData.GetProperty("success").GetBoolean());
            Assert.NotNull(verifyData.GetProperty("accessToken").GetString());
            Assert.False(verifyData.GetProperty("mfaRequired").GetBoolean());
        }

        [Fact]
        public void AuthController_GetOAuthProviders_ReturnsSupportedProviders()
        {
            var db = GetInMemoryDbContext();
            var controller = new AuthController(db);

            var result = controller.GetOAuthProviders();
            var okResult = Assert.IsType<OkObjectResult>(result);
            var providers = ToJson(okResult.Value);

            Assert.True(providers.GetArrayLength() >= 2);
        }

        [Fact]
        public async Task AuthController_OAuthLogin_WithValidTokenClaims_AuthenticatesOrProvisionsUser()
        {
            var db = GetInMemoryDbContext();
            var controller = new AuthController(db);

            // Create a mock Google OpenID Connect ID token
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestMockSecretKeyForOAuthOpenIdConnectSigning123!"));
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "google-sub-1029384756"),
                new Claim("email", "nambu.google@ffm.om"),
                new Claim("name", "Nambu Kamali"),
                new Claim("email_verified", "true")
            };
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256)
            };
            var idToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

            // Execute OAuth Login
            var loginResult = await controller.OAuthLogin(new AuthController.OAuthLoginRequest(
                Provider: "google",
                IdToken: idToken,
                AccessToken: null
            ));

            var okResult = Assert.IsType<OkObjectResult>(loginResult);
            var data = ToJson(okResult.Value);

            Assert.True(data.GetProperty("success").GetBoolean());
            Assert.NotNull(data.GetProperty("accessToken").GetString());

            // Verify user was provisioned in database
            var userInDb = await db.Users.FirstOrDefaultAsync(u => u.UsernameOrEmail == "nambu.google@ffm.om");
            Assert.NotNull(userInDb);
            Assert.Equal(Role.EMPLOYEE, userInDb.Role);
            Assert.Equal(AccountStatus.Active, userInDb.Status);
        }
    }
}
