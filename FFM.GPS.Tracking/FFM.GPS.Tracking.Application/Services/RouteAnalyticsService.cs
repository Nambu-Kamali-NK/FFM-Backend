using System;
using System.Collections.Generic;
using System.Linq;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Services;

namespace FFM.GPS.Tracking.Application.Services;

public class RouteAnalyticsService : IRouteAnalyticsService
{
    private readonly IGeofenceValidationService _geofenceValidationService;

    public RouteAnalyticsService(IGeofenceValidationService geofenceValidationService)
    {
        _geofenceValidationService = geofenceValidationService;
    }

    public TravelRouteHistoryResponseDto CalculateRouteHistory(
        int userId,
        string date,
        IReadOnlyList<LocationBreadcrumb> breadcrumbs,
        IEnumerable<Geofence> activeGeofences,
        bool includePolyline = true,
        int minDwellMinutes = 5)
    {
        var response = new TravelRouteHistoryResponseDto
        {
            UserId = userId,
            Date = date,
            Metrics = new RouteMetricsDto()
        };

        if (breadcrumbs == null || breadcrumbs.Count == 0)
        {
            return response;
        }

        // Sort chronologically
        var sorted = breadcrumbs.OrderBy(b => b.RecordedAt).ToList();
        response.Metrics.TotalBreadcrumbsCount = sorted.Count;
        response.Metrics.ShiftStartTime = sorted.First().RecordedAt;
        response.Metrics.ShiftEndTime = sorted.Last().RecordedAt;

        var totalDuration = (sorted.Last().RecordedAt - sorted.First().RecordedAt).TotalMinutes;
        response.Metrics.TotalDurationMinutes = Math.Max(0, (int)Math.Round(totalDuration));

        // Battery Drain
        var firstBattery = sorted.FirstOrDefault(b => b.BatteryPercentage.HasValue)?.BatteryPercentage;
        var lastBattery = sorted.LastOrDefault(b => b.BatteryPercentage.HasValue)?.BatteryPercentage;
        if (firstBattery.HasValue && lastBattery.HasValue && firstBattery.Value >= lastBattery.Value)
        {
            response.Metrics.BatteryDrainPercentage = firstBattery.Value - lastBattery.Value;
        }

        // Top Max Speed
        response.Metrics.MaxSpeedKmh = Math.Round((double)sorted.Max(b => b.SpeedKmh ?? 0m), 1);

        double totalDistanceMeters = 0.0;
        double movingDurationSeconds = 0.0;
        double stationaryDurationSeconds = 0.0;

        // 1. Sequential Haversine Calculation with Anti-Jitter Filtering
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var p1 = sorted[i];
            var p2 = sorted[i + 1];

            var distance = _geofenceValidationService.CalculateHaversineDistanceMeters(
                (double)p1.Latitude, (double)p1.Longitude,
                (double)p2.Latitude, (double)p2.Longitude);

            var speed = (double)(p2.SpeedKmh ?? 0m);
            var deltaSeconds = Math.Max(0, (p2.RecordedAt - p1.RecordedAt).TotalSeconds);

            // Jitter filter: ignore drift < 15 meters when stationary (speed < 2 km/h)
            if (distance < 15.0 && speed < 2.0)
            {
                stationaryDurationSeconds += deltaSeconds;
            }
            else
            {
                totalDistanceMeters += distance;
                movingDurationSeconds += deltaSeconds;
            }
        }

        response.Metrics.TotalDistanceKilometers = Math.Round(totalDistanceMeters / 1000.0, 2);
        response.Metrics.MovingDurationMinutes = (int)Math.Round(movingDurationSeconds / 60.0);
        response.Metrics.StationaryDurationMinutes = Math.Max(0, response.Metrics.TotalDurationMinutes - response.Metrics.MovingDurationMinutes);

        if (movingDurationSeconds > 0)
        {
            var movingHours = movingDurationSeconds / 3600.0;
            response.Metrics.AverageMovingSpeedKmh = Math.Round((totalDistanceMeters / 1000.0) / movingHours, 1);
        }

        // 2. Stop & Dwell Time Detection (Clusters within 40m for >= minDwellMinutes)
        response.Stops = DetectStops(sorted, activeGeofences, minDwellMinutes);

        // 3. Polyline Array
        if (includePolyline)
        {
            response.Polyline = sorted.Select(b => new RoutePointDto
            {
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                SpeedKmh = b.SpeedKmh,
                RecordedAt = b.RecordedAt
            }).ToList();
        }

        return response;
    }

    private List<RouteStopDto> DetectStops(List<LocationBreadcrumb> points, IEnumerable<Geofence> geofences, int minDwellMinutes)
    {
        var stops = new List<RouteStopDto>();
        if (points.Count < 2) return stops;

        int stopIndex = 1;
        int i = 0;

        while (i < points.Count)
        {
            var anchor = points[i];
            int j = i + 1;
            var cluster = new List<LocationBreadcrumb> { anchor };

            while (j < points.Count)
            {
                var candidate = points[j];
                var distFromAnchor = _geofenceValidationService.CalculateHaversineDistanceMeters(
                    (double)anchor.Latitude, (double)anchor.Longitude,
                    (double)candidate.Latitude, (double)candidate.Longitude);

                if (distFromAnchor <= 40.0)
                {
                    cluster.Add(candidate);
                    j++;
                }
                else
                {
                    break;
                }
            }

            var dwellMinutes = (cluster.Last().RecordedAt - cluster.First().RecordedAt).TotalMinutes;
            if (cluster.Count >= 2 && dwellMinutes >= minDwellMinutes)
            {
                var centroidLat = cluster.Average(c => c.Latitude);
                var centroidLon = cluster.Average(c => c.Longitude);

                // Match against active geofences
                string siteName = MatchGeofenceSite((double)centroidLat, (double)centroidLon, geofences) ?? "Client Site / Field Stop";

                stops.Add(new RouteStopDto
                {
                    StopIndex = stopIndex++,
                    SiteName = siteName,
                    Latitude = Math.Round(centroidLat, 7),
                    Longitude = Math.Round(centroidLon, 7),
                    ArrivedAt = cluster.First().RecordedAt,
                    DepartedAt = cluster.Last().RecordedAt,
                    DwellDurationMinutes = (int)Math.Round(dwellMinutes),
                    StopType = siteName != "Client Site / Field Stop" ? "GEOFENCE_SITE" : "CLIENT_VISIT"
                });

                i = j; // Move to next cluster
            }
            else
            {
                i++;
            }
        }

        return stops;
    }

    private string? MatchGeofenceSite(double lat, double lon, IEnumerable<Geofence>? geofences)
    {
        if (geofences == null) return null;

        foreach (var fence in geofences)
        {
            if (string.Equals(fence.Type, "CIRCULAR", StringComparison.OrdinalIgnoreCase) &&
                fence.CenterLatitude.HasValue && fence.CenterLongitude.HasValue && fence.RadiusMeters.HasValue)
            {
                if (_geofenceValidationService.IsPointInCircularGeofence(
                    lat, lon, fence.CenterLatitude.Value, fence.CenterLongitude.Value, fence.RadiusMeters.Value, fence.BufferMeters))
                {
                    return fence.Name;
                }
            }
            else if (string.Equals(fence.Type, "POLYGON", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(fence.PolygonCoordinatesJson))
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var vertices = System.Text.Json.JsonSerializer.Deserialize<CoordinateVertex[]>(fence.PolygonCoordinatesJson, options);
                    if (vertices != null && vertices.Length >= 3 && _geofenceValidationService.IsPointInPolygonGeofence(lat, lon, vertices))
                    {
                        return fence.Name;
                    }
                }
                catch
                {
                    // Malformed JSON polygon format ignored, evaluate remaining geofences
                }
            }
        }

        return null;
    }
}
