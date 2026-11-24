// File: LoginResponse.cs
namespace FFM.Identity.Access.Api.Models
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public bool MfaRequired { get; set; }
        public string? MfaChallengeId { get; set; }
    }
}
