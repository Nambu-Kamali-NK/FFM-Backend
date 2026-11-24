using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Api.Hubs;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FFM.GPS.Tracking.Api.Services;

public class SignalRRedisLocationPublisher : ILocationStreamPublisher
{
    private readonly IHubContext<TrackingHub, ITrackingClient> _hubContext;
    private readonly IConnectionMultiplexer? _redisMultiplexer;
    private readonly ILogger<SignalRRedisLocationPublisher> _logger;

    public SignalRRedisLocationPublisher(
        IHubContext<TrackingHub, ITrackingClient> hubContext,
        ILogger<SignalRRedisLocationPublisher> logger,
        IConnectionMultiplexer? redisMultiplexer = null)
    {
        _hubContext = hubContext;
        _logger = logger;
        _redisMultiplexer = redisMultiplexer;
    }

    public async Task PublishLocationAsync(LocationBreadcrumbStreamDto breadcrumb, CancellationToken cancellationToken = default)
    {
        // 1. Fast Path: In-Memory Redis Cache & Geospatial Indexing
        if (_redisMultiplexer != null && _redisMultiplexer.IsConnected)
        {
            try
            {
                var db = _redisMultiplexer.GetDatabase();
                var redisKey = $"tracking:user:{breadcrumb.UserId}:latest";

                var hashEntries = new HashEntry[]
                {
                    new("lat", (double)breadcrumb.Latitude),
                    new("lng", (double)breadcrumb.Longitude),
                    new("accuracy", (double)(breadcrumb.AccuracyMeters ?? 0)),
                    new("speed", (double)(breadcrumb.SpeedKmh ?? 0)),
                    new("heading", (double)(breadcrumb.HeadingDegrees ?? 0)),
                    new("battery", breadcrumb.BatteryPercentage ?? 0),
                    new("activity", breadcrumb.ActivityType),
                    new("recordedAt", breadcrumb.RecordedAt.ToString("O")),
                    new("transmittedAt", breadcrumb.TransmittedAt.ToString("O"))
                };

                await db.HashSetAsync(redisKey, hashEntries);
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(120));

                // Geospatial indexing for instant radius queries (GEOSEARCH)
                if (Math.Abs((double)breadcrumb.Latitude) <= 85.05112878 && Math.Abs((double)breadcrumb.Longitude) <= 180.0)
                {
                    await db.GeoAddAsync("tracking:tenant:default:geo", (double)breadcrumb.Longitude, (double)breadcrumb.Latitude, breadcrumb.UserId.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache update failed for user {UserId}. Falling back to SignalR broadcast.", breadcrumb.UserId);
            }
        }

        // 2. Fast Path: Broadcast to SignalR WebSocket Groups
        try
        {
            // Push to individual user channel (for user or direct supervisor tracking)
            await _hubContext.Clients.Group($"user_{breadcrumb.UserId}").ReceiveLocationUpdate(breadcrumb);

            // Push to supervisors/dispatchers global stream
            await _hubContext.Clients.Group("supervisors").ReceiveLocationUpdate(breadcrumb);

            _logger.LogDebug("Location update successfully streamed for User {UserId} at ({Lat}, {Lng})",
                breadcrumb.UserId, breadcrumb.Latitude, breadcrumb.Longitude);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast failed for user {UserId}", breadcrumb.UserId);
        }
    }

    public async Task PublishUserOfflineAsync(UserOfflineEventDto offlineEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.Group($"user_{offlineEvent.UserId}").UserWentOffline(offlineEvent);
            await _hubContext.Clients.Group("supervisors").UserWentOffline(offlineEvent);
            _logger.LogInformation("Dispatched offline notification for User {UserId} (Reason: {Reason})",
                offlineEvent.UserId, offlineEvent.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast offline event for User {UserId}", offlineEvent.UserId);
        }
    }

    public async Task PublishGeofenceAlertAsync(GeofenceAlertDto alert, CancellationToken cancellationToken = default)
    {
        // 1. Publish to Redis Pub/Sub Channel for external microservices (notifications, audit, dashboard)
        if (_redisMultiplexer != null && _redisMultiplexer.IsConnected)
        {
            try
            {
                var sub = _redisMultiplexer.GetSubscriber();
                var channel = StackExchange.Redis.RedisChannel.Literal("ffm:tracking:alerts:geofence");
                var alertJson = System.Text.Json.JsonSerializer.Serialize(alert);
                await sub.PublishAsync(channel, alertJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish geofence alert to Redis pub/sub channel for user {UserId}", alert.UserId);
            }
        }

        // 2. Broadcast real-time to SignalR WebSocket Clients
        try
        {
            // Notify user
            await _hubContext.Clients.Group($"user_{alert.UserId}").ReceiveGeofenceAlert(alert);

            // Notify dispatchers & supervisors
            await _hubContext.Clients.Group("supervisors").ReceiveGeofenceAlert(alert);

            _logger.LogInformation("Real-time geofence alert dispatched for User {UserId}: Type={BreachType}, Geofence={GeofenceName}",
                alert.UserId, alert.BreachType, alert.GeofenceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast of geofence alert failed for user {UserId}", alert.UserId);
        }
    }
}
