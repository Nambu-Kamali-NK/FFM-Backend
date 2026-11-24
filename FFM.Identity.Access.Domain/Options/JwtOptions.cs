// File: JwtOptions.cs
namespace FFM.Identity.Access.Domain.Options
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int ExpiryMinutes { get; set; }
        public int RefreshExpiryDays { get; set; }
        public string RsaPrivateKey { get; set; } = null!; // Base64 encoded PEM
    }
}
