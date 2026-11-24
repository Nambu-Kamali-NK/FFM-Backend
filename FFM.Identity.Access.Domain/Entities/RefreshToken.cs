// File: RefreshToken.cs
using System;

namespace FFM.Identity.Access.Domain.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; init; }
        public int UserId { get; init; }
        public string TokenHash { get; set; } = null!; // hash of the refresh token
        public string ClientId { get; set; } = null!; // client identifier
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public Guid? ReplacedById { get; set; }
    }
}
