// File: AuthAuditLog.cs
using System;

namespace FFM.Identity.Access.Domain.Entities
{
    public enum AuditEventType
    {
        LoginSuccess,
        LoginFailed,
        MfaChallengeCreated,
        MfaChallengeVerified,
        DeviceBindingFailed,
        AccountLocked,
        RefreshTokenIssued,
        RefreshTokenRevoked,
        PasswordResetRequested,
        PasswordResetCompleted
    }

    public class AuthAuditLog
    {
        public Guid Id { get; init; }
        public int UserId { get; init; }
        public AuditEventType EventType { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string IpAddress { get; set; } = null!;
        public string? DeviceId { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}
