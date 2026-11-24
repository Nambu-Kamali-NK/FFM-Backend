// File: UserDevice.cs
using System;

namespace FFM.Identity.Access.Domain.Entities
{
    public enum DeviceStatus
    {
        Pending,
        Active,
        Blocked,
        Revoked,
        Replaced
    }

    public class UserDevice
    {
        public Guid Id { get; init; }
        public int UserId { get; init; }
        public string DeviceUuid { get; set; } = null!; // normalized identifier
        public DeviceStatus Status { get; set; } = DeviceStatus.Pending;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; } // optional approval timestamp
    }
}
