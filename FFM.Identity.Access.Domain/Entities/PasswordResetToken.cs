using System;

namespace FFM.Identity.Access.Domain.Entities
{
    public class PasswordResetToken
    {
        public int Id { get; init; }
        public int UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTimeOffset? UsedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? IpAddress { get; set; }

        public User? User { get; set; }
    }
}
