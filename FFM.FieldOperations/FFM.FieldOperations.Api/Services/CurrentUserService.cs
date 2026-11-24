using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using FFM.FieldOperations.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FFM.FieldOperations.Api.Services
{
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
            if (user == null) return 0;

            var claim = user.FindFirst(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirst(ClaimTypes.NameIdentifier)
                ?? user.FindFirst("userId")
                ?? user.FindFirst("user_id");

            return int.TryParse(claim?.Value, out var id) ? id : 0;
        }

        public IEnumerable<string> GetRoles()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return Enumerable.Empty<string>();

            return user.FindAll(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value);
        }

        public bool IsInRole(string role)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return false;

            return user.IsInRole(role) ||
                   user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") &&
                                      c.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
        }
    }
}
