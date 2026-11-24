using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Interfaces;

namespace FFM.GPS.Tracking.Application.Services
{
    public class InMemoryGeofenceStateCache : IGeofenceStateCache
    {
        private readonly ConcurrentDictionary<string, string> _cache = new();

        public Task<string?> GetStateAsync(int userId, int geofenceId, CancellationToken cancellationToken = default)
        {
            var key = $"{userId}:{geofenceId}";
            _cache.TryGetValue(key, out var state);
            return Task.FromResult<string?>(state);
        }

        public Task SetStateAsync(int userId, int geofenceId, string state, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            var key = $"{userId}:{geofenceId}";
            _cache[key] = state;
            return Task.CompletedTask;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
