using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Api.Services;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Domain.Models;
using FFM.Identity.Access.Infrastructure.Data;

namespace FFM.Identity.Access.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IdentityDbContext _dbContext;

        public AuthController(IAuthService authService, IRefreshTokenService refreshTokenService, IdentityDbContext dbContext)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Authenticates a user and returns an access token + refresh token (or MFA challenge).
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            var clientId = Request.Headers["X-Client-Id"].FirstOrDefault() ?? "ffm-mobile";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _authService.AuthenticateAsync(request, clientId, ipAddress);
            if (response == null)
                return Unauthorized();

            return Ok(response);
        }

        /// <summary>
        /// Verifies a 6-digit MFA challenge code and issues full JWT tokens upon successful verification.
        /// </summary>
        [HttpPost("mfa/verify")]
        public async Task<ActionResult<LoginResponse>> VerifyMfa([FromBody] MfaVerifyRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MfaChallengeId) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(new { success = false, message = "Challenge ID and verification code are required." });
            }

            var clientId = Request.Headers["X-Client-Id"].FirstOrDefault() ?? "ffm-web";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, response, message) = await _authService.VerifyMfaAsync(request, clientId, ipAddress);
            if (!success || response == null)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(response);
        }

        /// <summary>
        /// Dispatches a fresh 6-digit MFA verification code for an active challenge.
        /// </summary>
        [HttpPost("mfa/resend")]
        public async Task<IActionResult> ResendMfa([FromBody] MfaResendRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MfaChallengeId))
            {
                return BadRequest(new { success = false, message = "Challenge ID is required." });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, message) = await _authService.ResendMfaCodeAsync(request.MfaChallengeId, ipAddress);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        /// <summary>
        /// Exchanges a valid refresh token for a new access token (and a new refresh token).
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request)
        {
            var response = await _refreshTokenService.RefreshAsync(request);
            if (response == null)
                return Unauthorized();

            return Ok(response);
        }

        /// <summary>
        /// Returns information about the currently authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserInfoResponse>> Me()
        {
            var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (subClaim == null)
                return Unauthorized();

            if (!int.TryParse(subClaim.Value, out var userId))
                return Unauthorized();

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            var response = new UserInfoResponse
            {
                Id = user.Id,
                UsernameOrEmail = user.UsernameOrEmail,
                MfaEnabled = user.MfaEnabled,
                Status = user.Status
            };
            return Ok(response);
        }

        /// <summary>
        /// Initiates password recovery by generating a reset token and dispatching a reset link.
        /// Anti-enumeration defense: Always returns 200 OK whether user exists or not.
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, message = "Email is required." });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _authService.ForgotPasswordAsync(request.Email, ipAddress);

            return Ok(new
            {
                success = true,
                message = "If an account exists with the provided email, a password reset link has been sent."
            });
        }

        /// <summary>
        /// Resets user password using a valid, non-expired reset token and revokes active sessions.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body cannot be empty." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { success = false, message = string.Join(" ", errors) });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, message) = await _authService.ResetPasswordAsync(request.Token, request.NewPassword, ipAddress);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        /// <summary>
        /// Changes the password for the currently authenticated user.
        /// Adheres strictly to Zero Path Parameters standard.
        /// </summary>
        [Authorize]
        [HttpPost("change-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Request body cannot be empty." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { success = false, message = string.Join(" ", errors) });
            }

            // Extract authenticated user ID from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("userId")?.Value
                           ?? User.FindFirst("user_id")?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                return Unauthorized(new { success = false, message = "User identity claim could not be determined from the access token." });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, message) = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ipAddress);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
    }
}
