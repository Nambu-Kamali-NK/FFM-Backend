using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FFM.GPS.Tracking.Infrastructure.Services
{
    public class RedisGeofenceStateCache : IGeofenceStateCache
    {
        private readonly IConnectionMultiplexer? _redisMultiplexer;
        private readonly ILogger<RedisGeofenceStateCache>? _logger;
        private readonly ConcurrentDictionary<string, string> _fallbackCache = new();

        public RedisGeofenceStateCache(
            IConnectionMultiplexer? redisMultiplexer = null,
            ILogger<RedisGeofenceStateCache>? logger = null)
        {
            _redisMultiplexer = redisMultiplexer;
            _logger = logger;
        }

        public async Task<string?> GetStateAsync(int userId, int geofenceId, CancellationToken cancellationToken = default)
        {
            var key = $"tracking:user:{userId}:geofence:{geofenceId}:state";

            if (_redisMultiplexer != null && _redisMultiplexer.IsConnected)
            {
                try
                {
                    var db = _redisMultiplexer.GetDatabase();
                    var val = await db.StringGetAsync(key);
                    if (val.HasValue)
                    {
                        return val.ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to read geofence state from Redis for user {UserId}, geofence {GeofenceId}", userId, geofenceId);
                }
            }

            _fallbackCache.TryGetValue(key, out var fallbackVal);
            return fallbackVal;
        }

        public async Task SetStateAsync(int userId, int geofenceId, string state, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            var key = $"tracking:user:{userId}:geofence:{geofenceId}:state";
            var ttl = expiry ?? TimeSpan.FromHours(12);

            _fallbackCache[key] = state;

            if (_redisMultiplexer != null && _redisMultiplexer.IsConnected)
            {
                try
                {
                    var db = _redisMultiplexer.GetDatabase();
                    await db.StringSetAsync(key, state, ttl);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to write geofence state to Redis for user {UserId}, geofence {GeofenceId}", userId, geofenceId);
                }
            }
        }
    }
}
