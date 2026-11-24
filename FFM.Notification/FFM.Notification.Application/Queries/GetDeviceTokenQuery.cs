using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Domain.Repositories;
using MediatR;

namespace FFM.Notification.Application.Queries;

public class GetDeviceTokenQuery : IRequest<GetDeviceTokenResponse?>
{
    public int EmployeeId { get; set; }

    public GetDeviceTokenQuery(int employeeId)
    {
        EmployeeId = employeeId;
    }
}

public class GetDeviceTokenQueryHandler : IRequestHandler<GetDeviceTokenQuery, GetDeviceTokenResponse?>
{
    private readonly IEmployeeDeviceRepository _deviceRepository;

    public GetDeviceTokenQueryHandler(IEmployeeDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<GetDeviceTokenResponse?> Handle(GetDeviceTokenQuery request, CancellationToken cancellationToken)
    {
        var device = await _deviceRepository.GetActiveByEmployeeIdAsync(request.EmployeeId, cancellationToken);
        if (device == null)
        {
            return null;
        }

        return new GetDeviceTokenResponse
        {
            EmployeeId = device.EmployeeId,
            DeviceId = device.DeviceId,
            Platform = device.Platform,
            DeviceModel = device.DeviceModel,
            FcmToken = device.FcmToken,
            IsActive = device.IsActive,
            LastActiveAt = device.LastActiveAt
        };
    }
}
