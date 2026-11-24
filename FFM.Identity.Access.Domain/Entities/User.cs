// File: User.cs
using System;
using System.Collections.Generic;

namespace FFM.Identity.Access.Domain.Entities
{
    public enum AccountStatus
    {
        PendingActivation,
        Active,
        Locked,
        Suspended,
        Blocked,
        Deactivated
    }

    public class User
    {
        public int Id { get; init; }
        public string UsernameOrEmail { get; set; } = null!; // normalized
        public string PasswordHash { get; set; } = null!; // Argon2id or bcrypt hash
        public AccountStatus Status { get; set; } = AccountStatus.Active;
        public Role Role { get; set; } = Role.EMPLOYEE;
        public bool MfaEnabled { get; set; } = false; // can be toggled per user
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
        public ICollection<UserDevice> Devices { get; set; } = new List<UserDevice>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    }
}
