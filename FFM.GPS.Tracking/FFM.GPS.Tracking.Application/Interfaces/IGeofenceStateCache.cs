using System;
using System.Threading;
using System.Threading.Tasks;

namespace FFM.GPS.Tracking.Application.Interfaces
{
    public interface IGeofenceStateCache
    {
        Task<string?> GetStateAsync(int userId, int geofenceId, CancellationToken cancellationToken = default);
        Task SetStateAsync(int userId, int geofenceId, string state, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    }
}
