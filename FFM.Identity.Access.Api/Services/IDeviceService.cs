using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.Identity.Access.Api.Models;

namespace FFM.Identity.Access.Api.Services
{
    public interface IDeviceService
    {
        Task<(bool Success, string Message, DeviceValidationResult Result)> ValidateDeviceAsync(
            int userId, string? deviceUuid, string? clientId, string? ipAddress = null);

        Task<List<DeviceDto>> GetUserDevicesAsync(int userId);

        Task<(bool Success, DeviceDto? Device, string Message)> RegisterDeviceAsync(
            int userId, RegisterDeviceRequest request, string? ipAddress = null);

        Task<(bool Success, string Message)> RevokeDeviceAsync(
            int userId, Guid deviceId, string? reason = null, string? ipAddress = null);

        Task<(bool Success, string Message)> ApproveDeviceAsync(
            Guid deviceId, int approverUserId, string? ipAddress = null);

        Task<List<DeviceDto>> GetAllDevicesAsync();
    }
}
