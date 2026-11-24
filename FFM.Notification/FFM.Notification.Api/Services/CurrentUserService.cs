using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FFM.Notification.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FFM.Notification.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user != null)
        {
            var claim = user.FindFirst(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirst(ClaimTypes.NameIdentifier)
                ?? user.FindFirst("userId")
                ?? user.FindFirst("user_id")
                ?? user.FindFirst("sub");

            if (int.TryParse(claim?.Value, out var id) && id > 0)
                return id;
        }

        // Development fallback to Admin (userId: 1)
        return 1;
    }

    public IEnumerable<string> GetRoles()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user != null && user.Identity?.IsAuthenticated == true)
        {
            var roles = user.FindAll(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value).ToList();
            if (roles.Count > 0) return roles;
        }

        return new[] { "ADMIN", "SYSTEM_ADMIN", "MANAGER", "EMPLOYEE" };
    }

    public bool IsInRole(string role)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user != null && user.Identity?.IsAuthenticated == true)
        {
            return user.IsInRole(role) ||
                   user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") &&
                                      c.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
        }

        return true; // Allow in development
    }
}
