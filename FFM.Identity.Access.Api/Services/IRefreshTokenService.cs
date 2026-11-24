using System;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;

namespace FFM.Identity.Access.Api.Services
{
    public interface IRefreshTokenService
    {
        Task<LoginResponse?> RefreshAsync(RefreshRequest request);
        Task RevokeUserTokensAsync(int userId);
    }
}
