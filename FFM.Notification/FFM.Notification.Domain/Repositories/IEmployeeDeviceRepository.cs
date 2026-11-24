using FFM.Notification.Domain.Entities;

namespace FFM.Notification.Domain.Repositories;

public interface IEmployeeDeviceRepository
{
    Task<EmployeeDevice?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<EmployeeDevice?> GetActiveByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);
    Task<List<EmployeeDevice>> GetAllActiveByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);
    Task AddAsync(EmployeeDevice device, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmployeeDevice device, CancellationToken cancellationToken = default);
    Task DeactivateOtherDevicesForEmployeeAsync(int employeeId, string currentDeviceId, string platform, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

