using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;
using FFM.Identity.Access.Domain.Entities;
using FFM.Identity.Access.Domain.Options;
using FFM.Identity.Access.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFM.Identity.Access.Api.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IdentityDbContext _dbContext;
        private readonly DeviceBindingOptions _options;
        private readonly ILogger<DeviceService> _logger;

        public DeviceService(
            IdentityDbContext dbContext,
            IOptions<DeviceBindingOptions> options,
            ILogger<DeviceService> logger)
        {
            _dbContext = dbContext;
            _options = options.Value ?? new DeviceBindingOptions();
            _logger = logger;
        }

        public async Task<(bool Success, string Message, DeviceValidationResult Result)> ValidateDeviceAsync(
            int userId, string? deviceUuid, string? clientId, string? ipAddress = null)
        {
            var effectiveClientId = clientId?.Trim().ToLowerInvariant() ?? "ffm-mobile";
            var requiresBinding = _options.ClientsRequiringBinding.Any(c => c.Equals(effectiveClientId, StringComparison.OrdinalIgnoreCase))
                               || !string.IsNullOrWhiteSpace(deviceUuid);

            if (!requiresBinding)
            {
                return (true, "Device binding not required for web client.", DeviceValidationResult.Valid);
            }

            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return (false, "Device identifier is required for mobile authentication.", DeviceValidationResult.MissingDeviceId);
            }

            var cleanUuid = deviceUuid.Trim();

            // Fetch existing devices for user
            var userDevices = await _dbContext.UserDevices
                .Where(d => d.UserId == userId)
                .ToListAsync();

            if (userDevices.Count == 0)
            {
                // First device registered: auto-bind as Active
                var newDevice = new UserDevice
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DeviceUuid = cleanUuid,
                    Status = DeviceStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ApprovedAt = DateTimeOffset.UtcNow
                };

                _dbContext.UserDevices.Add(newDevice);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Initial device {DeviceUuid} bound to user {UserId}", cleanUuid, userId);
                return (true, "Initial mobile device successfully registered and bound.", DeviceValidationResult.BoundNewDevice);
            }

            // Find matching device
            var existingDevice = userDevices.FirstOrDefault(d => d.DeviceUuid.Equals(cleanUuid, StringComparison.OrdinalIgnoreCase));

            if (existingDevice != null)
            {
                if (existingDevice.Status == DeviceStatus.Active)
                {
                    return (true, "Device verified.", DeviceValidationResult.Valid);
                }

                if (existingDevice.Status == DeviceStatus.Pending)
                {
                    return (false, "Device registration is pending manager or administrator approval.", DeviceValidationResult.PendingApproval);
                }

                return (false, $"Device access is {existingDevice.Status}. Contact your administrator.", DeviceValidationResult.Blocked);
            }

            // Device mismatch: user already has an active device, but requested from an unregistered device
            _dbContext.AuthAuditLogs.Add(new AuthAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = AuditEventType.DeviceBindingFailed,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = ipAddress ?? "unknown",
                DeviceId = cleanUuid,
                AdditionalInfo = $"Device binding mismatch: device {cleanUuid} not bound to user {userId}"
            });
            await _dbContext.SaveChangesAsync();

            _logger.LogWarning("Unauthorized device {DeviceUuid} attempted sign-in for user {UserId}", cleanUuid, userId);
            return (false, "Device mismatch: This account is locked to another authorized mobile device. Contact your administrator to transfer devices.", DeviceValidationResult.DeviceMismatch);
        }

        public async Task<List<DeviceDto>> GetUserDevicesAsync(int userId)
        {
            return await _dbContext.UserDevices
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DeviceDto
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    DeviceUuid = d.DeviceUuid,
                    Status = d.Status.ToString(),
                    CreatedAt = d.CreatedAt,
                    ApprovedAt = d.ApprovedAt
                })
                .ToListAsync();
        }

        public async Task<(bool Success, DeviceDto? Device, string Message)> RegisterDeviceAsync(
            int userId, RegisterDeviceRequest request, string? ipAddress = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DeviceUuid))
            {
                return (false, null, "Device UUID is required.");
            }

            var cleanUuid = request.DeviceUuid.Trim();

            // Check if device already registered for any user
            var existing = await _dbContext.UserDevices.FirstOrDefaultAsync(d => d.DeviceUuid == cleanUuid);
            if (existing != null)
            {
                if (existing.UserId == userId)
                {
                    return (true, new DeviceDto
                    {
                        Id = existing.Id,
                        UserId = existing.UserId,
                        DeviceUuid = existing.DeviceUuid,
                        Status = existing.Status.ToString(),
                        CreatedAt = existing.CreatedAt,
                        ApprovedAt = existing.ApprovedAt
                    }, "Device already registered for this user.");
                }

                return (false, null, "This device is already bound to another employee account.");
            }

            // If user has no active devices, set as Active; otherwise Pending approval
            var hasActiveDevice = await _dbContext.UserDevices.AnyAsync(d => d.UserId == userId && d.Status == DeviceStatus.Active);
            var initialStatus = hasActiveDevice ? DeviceStatus.Pending : DeviceStatus.Active;

            var device = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceUuid = cleanUuid,
                Status = initialStatus,
                CreatedAt = DateTimeOffset.UtcNow,
                ApprovedAt = initialStatus == DeviceStatus.Active ? DateTimeOffset.UtcNow : null
            };

            _dbContext.UserDevices.Add(device);
            await _dbContext.SaveChangesAsync();

            return (true, new DeviceDto
            {
                Id = device.Id,
                UserId = device.UserId,
                DeviceUuid = device.DeviceUuid,
                Status = device.Status.ToString(),
                CreatedAt = device.CreatedAt,
                ApprovedAt = device.ApprovedAt
            }, initialStatus == DeviceStatus.Active
                ? "Device successfully registered and active."
                : "Device registered and pending administrator approval.");
        }

        public async Task<(bool Success, string Message)> RevokeDeviceAsync(
            int userId, Guid deviceId, string? reason = null, string? ipAddress = null)
        {
            var device = await _dbContext.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);
            if (device == null)
            {
                return (false, "Device not found or not owned by this account.");
            }

            device.Status = DeviceStatus.Revoked;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Device {DeviceId} revoked for user {UserId}. Reason: {Reason}", deviceId, userId, reason);
            return (true, "Device access has been revoked successfully.");
        }

        public async Task<(bool Success, string Message)> ApproveDeviceAsync(
            Guid deviceId, int approverUserId, string? ipAddress = null)
        {
            var device = await _dbContext.UserDevices.FindAsync(deviceId);
            if (device == null)
            {
                return (false, "Device not found.");
            }

            // Revoke any previous active devices for this user (single active device policy)
            var otherActive = await _dbContext.UserDevices
                .Where(d => d.UserId == device.UserId && d.Id != device.Id && d.Status == DeviceStatus.Active)
                .ToListAsync();

            foreach (var d in otherActive)
            {
                d.Status = DeviceStatus.Replaced;
            }

            device.Status = DeviceStatus.Active;
            device.ApprovedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Device {DeviceId} approved by user {ApproverId}", deviceId, approverUserId);
            return (true, "Device approved and activated. Previous devices were replaced.");
        }

        public async Task<List<DeviceDto>> GetAllDevicesAsync()
        {
            return await _dbContext.UserDevices
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DeviceDto
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    DeviceUuid = d.DeviceUuid,
                    Status = d.Status.ToString(),
                    CreatedAt = d.CreatedAt,
                    ApprovedAt = d.ApprovedAt
                })
                .ToListAsync();
        }
    }
}
