using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using FFM.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.Notification.Infrastructure.Repositories;

public class EmployeeDeviceRepository : IEmployeeDeviceRepository
{
    private readonly NotificationDbContext _context;

    public EmployeeDeviceRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeDevice?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, cancellationToken);
    }

    public async Task<EmployeeDevice?> GetActiveByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDevices
            .Where(d => d.EmployeeId == employeeId && d.IsActive)
            .OrderByDescending(d => d.LastActiveAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<EmployeeDevice>> GetAllActiveByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeDevices
            .AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && d.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EmployeeDevice device, CancellationToken cancellationToken = default)
    {
        await _context.EmployeeDevices.AddAsync(device, cancellationToken);
    }

    public Task UpdateAsync(EmployeeDevice device, CancellationToken cancellationToken = default)
    {
        _context.EmployeeDevices.Update(device);
        return Task.CompletedTask;
    }

    public async Task DeactivateOtherDevicesForEmployeeAsync(int employeeId, string currentDeviceId, string platform, CancellationToken cancellationToken = default)
    {
        var otherDevices = await _context.EmployeeDevices
            .Where(d => d.EmployeeId == employeeId && d.DeviceId != currentDeviceId && d.Platform == platform && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in otherDevices)
        {
            device.Deactivate();
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

