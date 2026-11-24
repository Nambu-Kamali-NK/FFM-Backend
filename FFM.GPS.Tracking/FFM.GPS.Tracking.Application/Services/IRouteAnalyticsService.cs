using System.Collections.Generic;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Application.Services;

public interface IRouteAnalyticsService
{
    TravelRouteHistoryResponseDto CalculateRouteHistory(
        int userId,
        string date,
        IReadOnlyList<LocationBreadcrumb> breadcrumbs,
        IEnumerable<Geofence> activeGeofences,
        bool includePolyline = true,
        int minDwellMinutes = 5);
}
