using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Queries;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Application.Handlers;

public class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, DeviceRegistrationResponse>
{
    private readonly IEmployeeDeviceRepository _repository;
    private readonly ILogger<RegisterDeviceCommandHandler> _logger;

    public RegisterDeviceCommandHandler(
        IEmployeeDeviceRepository repository,
        ILogger<RegisterDeviceCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<DeviceRegistrationResponse> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var normalizedPlatform = request.Platform.Trim().ToUpperInvariant();

        // 1. Check if the physical hardware device is already registered
        var existingDevice = await _repository.GetByDeviceIdAsync(request.DeviceId, cancellationToken);

        EmployeeDevice targetDevice;

        if (existingDevice != null)
        {
            if (existingDevice.EmployeeId == request.EmployeeId)
            {
                // Scenario A: Same employee updating token/app version
                _logger.LogInformation("Refreshing push token for Employee {EmployeeId} on Device {DeviceId}", request.EmployeeId, request.DeviceId);
                existingDevice.UpdateToken(request.FcmToken, request.DeviceModel, request.OsVersion, request.AppVersion);
            }
            else
            {
                // Scenario B: Hardware handover to a new employee
                _logger.LogInformation("Hardware handover detected for Device {DeviceId}. Reassigning from Employee {OldEmployeeId} to {NewEmployeeId}",
                    request.DeviceId, existingDevice.EmployeeId, request.EmployeeId);
                existingDevice.ReassignEmployee(request.EmployeeId, request.FcmToken, request.DeviceModel, request.OsVersion, request.AppVersion);
            }

            // Ensure Single-Active policy on same platform
            await _repository.DeactivateOtherDevicesForEmployeeAsync(request.EmployeeId, request.DeviceId, normalizedPlatform, cancellationToken);
            await _repository.UpdateAsync(existingDevice, cancellationToken);
            targetDevice = existingDevice;
        }
        else
        {
            // Scenario C: First time hardware registration
            _logger.LogInformation("Registering new device hardware {DeviceId} for Employee {EmployeeId}", request.DeviceId, request.EmployeeId);
            
            // Deactivate any prior devices for this employee on this platform
            await _repository.DeactivateOtherDevicesForEmployeeAsync(request.EmployeeId, request.DeviceId, normalizedPlatform, cancellationToken);

            targetDevice = new EmployeeDevice(
                request.EmployeeId,
                request.DeviceId,
                normalizedPlatform,
                request.DeviceModel,
                request.OsVersion,
                request.AppVersion,
                request.FcmToken
            );

            await _repository.AddAsync(targetDevice, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return new DeviceRegistrationResponse
        {
            DeviceRegistrationId = targetDevice.Id,
            EmployeeId = targetDevice.EmployeeId,
            DeviceId = targetDevice.DeviceId,
            Platform = targetDevice.Platform,
            DeviceModel = targetDevice.DeviceModel,
            OsVersion = targetDevice.OsVersion,
            AppVersion = targetDevice.AppVersion,
            IsActive = targetDevice.IsActive,
            RegisteredAt = targetDevice.RegisteredAt,
            LastActiveAt = targetDevice.LastActiveAt
        };
    }
}

public class DeregisterDeviceCommandHandler : IRequestHandler<DeregisterDeviceCommand, bool>
{
    private readonly IEmployeeDeviceRepository _repository;
    private readonly ILogger<DeregisterDeviceCommandHandler> _logger;

    public DeregisterDeviceCommandHandler(
        IEmployeeDeviceRepository repository,
        ILogger<DeregisterDeviceCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeregisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await _repository.GetByDeviceIdAsync(request.DeviceId, cancellationToken);
        if (device == null || device.EmployeeId != request.EmployeeId)
        {
            // Idempotent: return true if already inactive or device doesn't belong to employee
            return true;
        }

        _logger.LogInformation("Deregistering device {DeviceId} for Employee {EmployeeId} upon logout", request.DeviceId, request.EmployeeId);
        device.Deactivate();
        await _repository.UpdateAsync(device, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class GetActiveDeviceQueryHandler : IRequestHandler<GetActiveDeviceQuery, DeviceRegistrationResponse?>
{
    private readonly IEmployeeDeviceRepository _repository;

    public GetActiveDeviceQueryHandler(IEmployeeDeviceRepository repository)
    {
        _repository = repository;
    }

    public async Task<DeviceRegistrationResponse?> Handle(GetActiveDeviceQuery request, CancellationToken cancellationToken)
    {
        var device = await _repository.GetActiveByEmployeeIdAsync(request.EmployeeId, cancellationToken);
        if (device == null)
            return null;

        return new DeviceRegistrationResponse
        {
            DeviceRegistrationId = device.Id,
            EmployeeId = device.EmployeeId,
            DeviceId = device.DeviceId,
            Platform = device.Platform,
            DeviceModel = device.DeviceModel,
            OsVersion = device.OsVersion,
            AppVersion = device.AppVersion,
            IsActive = device.IsActive,
            RegisteredAt = device.RegisteredAt,
            LastActiveAt = device.LastActiveAt
        };
    }
}

