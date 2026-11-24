using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FFM.GPS.Tracking.Infrastructure.Repositories;

public class BreadcrumbRepository : IBreadcrumbRepository
{
    private readonly GpsDbContext _dbContext;

    public BreadcrumbRepository(GpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(LocationBreadcrumb breadcrumb, CancellationToken cancellationToken = default)
    {
        await _dbContext.LocationBreadcrumbs.AddAsync(breadcrumb, cancellationToken);
    }

    public async Task<IEnumerable<LocationBreadcrumb>> GetByUserAndDateAsync(int userId, DateTime date, CancellationToken cancellationToken = default)
    {
        var startOfDay = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var endOfDay = startOfDay.AddDays(1);

        return await _dbContext.LocationBreadcrumbs
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.RecordedAt >= startOfDay && b.RecordedAt < endOfDay)
            .OrderBy(b => b.RecordedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationBreadcrumb?> GetLatestByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LocationBreadcrumbs
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
