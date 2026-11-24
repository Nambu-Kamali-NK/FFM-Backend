using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace FFM.Workforce.Infrastructure.Services
{
    /// <summary>
    /// Reads the JWT claims from the current HttpContext. The Identity service places the user id in the "sub" claim
    /// and role(s) in the "role" claim (may be multiple). This class is registered as scoped.
    /// </summary>
    public class CurrentUserService : FFM.Workforce.Application.Interfaces.ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst("userId")
                ?? user?.FindFirst("user_id")
                ?? user?.FindFirst(ClaimTypes.NameIdentifier)
                ?? user?.FindFirst("sub");
            if (sub == null || !int.TryParse(sub.Value, out var id))
                throw new InvalidOperationException("UserId claim missing or not an integer.");
            return id;
        }

        public IEnumerable<string> GetRoles()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return Enumerable.Empty<string>();
            // "role" claim may appear multiple times or as a single comma‑separated value.
            var roleClaims = user.FindAll("role").Select(c => c.Value);
            if (!roleClaims.Any())
            {
                // fallback to ClaimTypes.Role (standard)
                roleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
            }
            // Split any comma‑separated values
            return roleClaims.SelectMany(rc => rc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                             .Select(r => r.Trim())
                             .Where(r => !string.IsNullOrWhiteSpace(r));
        }
    }
}
