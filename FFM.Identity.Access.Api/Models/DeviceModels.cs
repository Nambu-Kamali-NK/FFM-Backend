using System;
using FFM.Identity.Access.Domain.Entities;

namespace FFM.Identity.Access.Api.Models
{
    public enum DeviceValidationResult
    {
        Valid,
        BoundNewDevice,
        DeviceMismatch,
        PendingApproval,
        Blocked,
        MissingDeviceId
    }

    public class RegisterDeviceRequest
    {
        public string DeviceUuid { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? OsVersion { get; set; }
    }

    public class RevokeDeviceRequest
    {
        public Guid DeviceId { get; set; }
        public string? Reason { get; set; }
    }

    public class ApproveDeviceRequest
    {
        public Guid DeviceId { get; set; }
    }

    public class DeviceDto
    {
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public string DeviceUuid { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
    }
}
