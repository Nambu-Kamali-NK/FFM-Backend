// File: RateLimitOptions.cs
namespace FFM.Identity.Access.Domain.Options
{
    public class RateLimitOptions
    {
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;
    }
}
