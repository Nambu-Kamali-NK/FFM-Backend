// File: MfaChallenge.cs
using System;

namespace FFM.Identity.Access.Domain.Entities
{
    public class MfaChallenge
    {
        public Guid Id { get; init; }
        public int UserId { get; init; }
        public string OtpHash { get; set; } = null!; // HMAC of OTP
        public DateTimeOffset ExpiresAt { get; set; }
        public int FailedAttempts { get; set; }
        public bool Verified { get; set; }
        public string? ChallengeId => Id.ToString(); // convenience
    }
}
