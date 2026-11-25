using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Domain.Models;

namespace FFM.Identity.Access.Api.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> AuthenticateAsync(LoginRequest request, string? clientId = null, string? ipAddress = null);
        Task<(bool Success, LoginResponse? Response, string Message)> VerifyMfaAsync(MfaVerifyRequest request, string? clientId = null, string? ipAddress = null);
        Task<(bool Success, string Message)> ResendMfaCodeAsync(string mfaChallengeId, string? ipAddress = null);
        Task<MfaSetupResponse> SetupMfaAsync(int userId);
        Task<(bool Success, string Message)> EnableMfaAsync(int userId, MfaEnableRequest request);
        Task<(bool Success, string Message)> DisableMfaAsync(int userId, MfaDisableRequest request);
        Task<LoginResponse> IssueTokensAsync(User user, string effectiveClientId, string? ipAddress = null);
        Task<bool> ForgotPasswordAsync(string email, string? ipAddress = null);
        Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword, string? ipAddress = null);
        Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string? ipAddress = null);
    }
}
