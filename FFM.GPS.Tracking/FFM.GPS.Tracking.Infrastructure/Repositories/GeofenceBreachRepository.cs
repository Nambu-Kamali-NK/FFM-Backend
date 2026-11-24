using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FFM.GPS.Tracking.Infrastructure.Repositories
{
    public class GeofenceBreachRepository : IGeofenceBreachRepository
    {
        private readonly GpsDbContext _context;

        public GeofenceBreachRepository(GpsDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(GeofenceBreachEvent breachEvent, CancellationToken cancellationToken = default)
        {
            await _context.GeofenceBreachEvents.AddAsync(breachEvent, cancellationToken);
        }

        public async Task<GeofenceBreachEvent?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return await _context.GeofenceBreachEvents
                .Include(b => b.Geofence)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        }

        public async Task<List<GeofenceBreachEvent>> GetBreachesAsync(
            int? userId,
            int? geofenceId,
            string? breachType,
            bool? isAcknowledged,
            DateTime? fromDate,
            DateTime? toDate,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = BuildQuery(userId, geofenceId, breachType, isAcknowledged, fromDate, toDate);

            return await query
                .OrderByDescending(b => b.TriggeredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetBreachesCountAsync(
            int? userId,
            int? geofenceId,
            string? breachType,
            bool? isAcknowledged,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken cancellationToken = default)
        {
            var query = BuildQuery(userId, geofenceId, breachType, isAcknowledged, fromDate, toDate);
            return await query.CountAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        private IQueryable<GeofenceBreachEvent> BuildQuery(
            int? userId,
            int? geofenceId,
            string? breachType,
            bool? isAcknowledged,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _context.GeofenceBreachEvents.AsNoTracking().AsQueryable();

            if (userId.HasValue && userId.Value > 0)
                query = query.Where(b => b.UserId == userId.Value);

            if (geofenceId.HasValue && geofenceId.Value > 0)
                query = query.Where(b => b.GeofenceId == geofenceId.Value);

            if (!string.IsNullOrWhiteSpace(breachType))
                query = query.Where(b => b.BreachType == breachType.ToUpper());

            if (isAcknowledged.HasValue)
                query = query.Where(b => b.IsAcknowledged == isAcknowledged.Value);

            if (fromDate.HasValue)
                query = query.Where(b => b.TriggeredAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(b => b.TriggeredAt <= toDate.Value);

            return query;
        }
    }
}
