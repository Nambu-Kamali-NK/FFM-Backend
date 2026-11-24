using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Services;
using FFM.GPS.Tracking.Domain.Interfaces;
using MediatR;

namespace FFM.GPS.Tracking.Application.Queries;

public class GetTravelRouteHistoryQuery : IRequest<TravelRouteHistoryResponseDto>
{
    public int UserId { get; set; }
    public string Date { get; set; } = string.Empty;
    public bool IncludePolyline { get; set; } = true;
    public int MinDwellMinutes { get; set; } = 5;

    public GetTravelRouteHistoryQuery(int userId, string date, bool includePolyline = true, int minDwellMinutes = 5)
    {
        UserId = userId;
        Date = date;
        IncludePolyline = includePolyline;
        MinDwellMinutes = minDwellMinutes;
    }
}

public class GetTravelRouteHistoryQueryHandler : IRequestHandler<GetTravelRouteHistoryQuery, TravelRouteHistoryResponseDto>
{
    private readonly IBreadcrumbRepository _breadcrumbRepository;
    private readonly IGeofenceRepository _geofenceRepository;
    private readonly IRouteAnalyticsService _analyticsService;

    public GetTravelRouteHistoryQueryHandler(
        IBreadcrumbRepository breadcrumbRepository,
        IGeofenceRepository geofenceRepository,
        IRouteAnalyticsService analyticsService)
    {
        _breadcrumbRepository = breadcrumbRepository;
        _geofenceRepository = geofenceRepository;
        _analyticsService = analyticsService;
    }

    public async Task<TravelRouteHistoryResponseDto> Handle(GetTravelRouteHistoryQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId <= 0)
        {
            throw new ArgumentException("UserId must be a positive integer.");
        }

        if (!DateTime.TryParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            throw new ArgumentException("The date must be supplied in ISO format 'YYYY-MM-DD'.");
        }

        if (parsedDate.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Cannot query travel route history for future dates.");
        }

        // Fetch breadcrumbs for the day using AsNoTracking
        var breadcrumbs = await _breadcrumbRepository.GetByUserAndDateAsync(request.UserId, parsedDate, cancellationToken);
        var breadcrumbList = breadcrumbs.ToList();

        // Fetch active geofences for naming stops
        var geofences = await _geofenceRepository.GetAllActiveAsync();

        // Calculate and return analytics metrics, stops, and polyline
        return _analyticsService.CalculateRouteHistory(
            request.UserId,
            request.Date,
            breadcrumbList,
            geofences,
            request.IncludePolyline,
            request.MinDwellMinutes);
    }
}
