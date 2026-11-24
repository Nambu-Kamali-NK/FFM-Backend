using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Domain.Interfaces;

public interface IBreadcrumbRepository
{
    Task AddAsync(LocationBreadcrumb breadcrumb, CancellationToken cancellationToken = default);
    Task<IEnumerable<LocationBreadcrumb>> GetByUserAndDateAsync(int userId, DateTime date, CancellationToken cancellationToken = default);
    Task<LocationBreadcrumb?> GetLatestByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
