using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FFM.Workforce.Infrastructure;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly WorkforceDbContext _dbContext;
    public static readonly SymmetricSecurityKey JwtSecret =
        new(Encoding.UTF8.GetBytes("SuperSecretDevelopmentKey123!_FFM_Production_Key_2026_Secure"));

    public AuthController(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public record LoginRequest(string Username, string Password);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { success = false, message = "Username and password are required." });
        }

        var normalized = request.Username.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UsernameOrEmail.ToLower() == normalized);

        if (user == null)
        {
            return Unauthorized(new { success = false, message = "Invalid email address or password." });
        }

        bool valid = false;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            valid = false;
        }

        if (!valid)
        {
            return Unauthorized(new { success = false, message = "Invalid email address or password." });
        }

        var roleName = user.Role.ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("userId", user.Id.ToString()),
            new(ClaimTypes.Email, user.UsernameOrEmail),
            new(ClaimTypes.Name, user.UsernameOrEmail),
            new(ClaimTypes.Role, roleName),
            new("role", roleName)
        };

        var creds = new SigningCredentials(JwtSecret, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            success = true,
            accessToken = tokenString,
            mfaRequired = false,
            user = new
            {
                id = user.Id,
                username = user.UsernameOrEmail,
                role = roleName
            }
        });
    }
}
