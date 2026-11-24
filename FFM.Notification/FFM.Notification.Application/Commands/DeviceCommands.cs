using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Commands;

public record RegisterDeviceCommand(
    int EmployeeId,
    string DeviceId,
    string Platform,
    string DeviceModel,
    string OsVersion,
    string AppVersion,
    string FcmToken
) : IRequest<DeviceRegistrationResponse>;

public record DeregisterDeviceCommand(
    int EmployeeId,
    string DeviceId
) : IRequest<bool>;

