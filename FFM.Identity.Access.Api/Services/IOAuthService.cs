using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;

namespace FFM.Identity.Access.Api.Services
{
    public interface IOAuthService
    {
        Task<(bool Success, LoginResponse? Response, string Message)> AuthenticateOAuthUserAsync(
            OAuthLoginRequest request, string? clientId = null, string? ipAddress = null);

        Task<(bool Success, OAuthUserInfo? UserInfo, string Message)> ValidateTokenAsync(
            string provider, string idToken, string? accessToken = null);

        List<OAuthProviderDto> GetSupportedProviders();
    }
}
