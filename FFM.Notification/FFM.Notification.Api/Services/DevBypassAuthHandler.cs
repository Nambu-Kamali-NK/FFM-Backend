using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFM.Notification.Api.Services;

public class DevBypassAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevBypassAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim("userId", "1"),
            new Claim("user_id", "1"),
            new Claim("sub", "1"),
            new Claim(ClaimTypes.Name, "Admin Developer"),
            new Claim(ClaimTypes.Role, "ADMIN"),
            new Claim(ClaimTypes.Role, "SYSTEM_ADMIN"),
            new Claim(ClaimTypes.Role, "MANAGER"),
            new Claim(ClaimTypes.Role, "EMPLOYEE")
        };

        var identity = new ClaimsIdentity(claims, "DevBypass");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "DevBypass");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

