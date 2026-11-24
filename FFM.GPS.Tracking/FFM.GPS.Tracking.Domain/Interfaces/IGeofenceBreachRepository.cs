using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Domain.Interfaces
{
    public interface IGeofenceBreachRepository
    {
        Task AddAsync(GeofenceBreachEvent breachEvent, CancellationToken cancellationToken = default);
        Task<GeofenceBreachEvent?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<List<GeofenceBreachEvent>> GetBreachesAsync(
            int? userId,
            int? geofenceId,
            string? breachType,
            bool? isAcknowledged,
            DateTime? fromDate,
            DateTime? toDate,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<int> GetBreachesCountAsync(
            int? userId,
            int? geofenceId,
            string? breachType,
            bool? isAcknowledged,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
