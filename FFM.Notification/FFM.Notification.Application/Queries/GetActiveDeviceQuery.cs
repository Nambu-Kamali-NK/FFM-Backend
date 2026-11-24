using FFM.Notification.Application.DTOs;
using MediatR;

namespace FFM.Notification.Application.Queries;

public record GetActiveDeviceQuery(int EmployeeId) : IRequest<DeviceRegistrationResponse?>;

