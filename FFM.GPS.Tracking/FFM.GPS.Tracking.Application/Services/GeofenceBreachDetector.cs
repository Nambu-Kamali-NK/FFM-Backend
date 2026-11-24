using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Interfaces;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using FFM.GPS.Tracking.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FFM.GPS.Tracking.Application.Services
{
    public class GeofenceBreachDetector : IGeofenceBreachDetector
    {
        private readonly IGeofenceRepository _geofenceRepository;
        private readonly IGeofenceBreachRepository _breachRepository;
        private readonly IGeofenceValidationService _validationService;
        private readonly IGeofenceStateCache _stateCache;
        private readonly ILocationStreamPublisher? _streamPublisher;
        private readonly ILogger<GeofenceBreachDetector>? _logger;

        public const string StateInside = "INSIDE";
        public const string StateOutside = "OUTSIDE";

        public GeofenceBreachDetector(
            IGeofenceRepository geofenceRepository,
            IGeofenceBreachRepository breachRepository,
            IGeofenceValidationService validationService,
            IGeofenceStateCache stateCache,
            ILocationStreamPublisher? streamPublisher = null,
            ILogger<GeofenceBreachDetector>? logger = null)
        {
            _geofenceRepository = geofenceRepository;
            _breachRepository = breachRepository;
            _validationService = validationService;
            _stateCache = stateCache;
            _streamPublisher = streamPublisher;
            _logger = logger;
        }

        public async Task<List<GeofenceAlertDto>> EvaluateBreadcrumbAsync(LocationBreadcrumb breadcrumb, CancellationToken cancellationToken = default)
        {
            var alerts = new List<GeofenceAlertDto>();

            // Fetch geofences assigned to this user
            var assignedGeofences = (await _geofenceRepository.GetAssignedGeofencesByEmployeeIdAsync(breadcrumb.UserId)).ToList();
            if (!assignedGeofences.Any())
            {
                return alerts;
            }

            var lat = (double)breadcrumb.Latitude;
            var lng = (double)breadcrumb.Longitude;

            foreach (var geofence in assignedGeofences)
            {
                if (!geofence.IsActive)
                    continue;

                bool isInside = false;
                double? distanceFromBoundary = null;

                if (string.Equals(geofence.Type, "CIRCULAR", StringComparison.OrdinalIgnoreCase))
                {
                    if (geofence.CenterLatitude.HasValue && geofence.CenterLongitude.HasValue && geofence.RadiusMeters.HasValue)
                    {
                        isInside = _validationService.IsPointInCircularGeofence(
                            lat,
                            lng,
                            geofence.CenterLatitude.Value,
                            geofence.CenterLongitude.Value,
                            geofence.RadiusMeters.Value,
                            geofence.BufferMeters);

                        var distanceFromCenter = _validationService.CalculateHaversineDistanceMeters(
                            lat,
                            lng,
                            geofence.CenterLatitude.Value,
                            geofence.CenterLongitude.Value);

                        distanceFromBoundary = Math.Round(Math.Max(0.0, distanceFromCenter - (geofence.RadiusMeters.Value + geofence.BufferMeters)), 2);
                    }
                }
                else if (string.Equals(geofence.Type, "POLYGON", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(geofence.PolygonCoordinatesJson))
                    {
                        try
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var vertices = JsonSerializer.Deserialize<CoordinateVertex[]>(geofence.PolygonCoordinatesJson, options);
                            if (vertices != null && vertices.Length > 0)
                            {
                                isInside = _validationService.IsPointInPolygonGeofence(lat, lng, vertices);
                                distanceFromBoundary = isInside ? 0.0 : 10.0; // Approximation for polygon
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to deserialize polygon coordinates for geofence {GeofenceId}", geofence.Id);
                        }
                    }
                }

                // Check prior cached state
                var previousState = await _stateCache.GetStateAsync(breadcrumb.UserId, geofence.Id, cancellationToken);
                var currentState = isInside ? StateInside : StateOutside;

                if (previousState == null)
                {
                    // First recorded position for this user & geofence
                    await _stateCache.SetStateAsync(breadcrumb.UserId, geofence.Id, currentState, TimeSpan.FromHours(12), cancellationToken);

                    if (!isInside)
                    {
                        // User is outside their assigned geofence on first check: trigger EXIT breach
                        var breachEvent = new GeofenceBreachEvent(
                            breadcrumb.UserId,
                            geofence.Id,
                            geofence.Name,
                            geofence.LocationRef,
                            "EXIT",
                            breadcrumb.Latitude,
                            breadcrumb.Longitude,
                            breadcrumb.AccuracyMeters,
                            distanceFromBoundary,
                            breadcrumb.SpeedKmh,
                            breadcrumb.RecordedAt);

                        await _breachRepository.AddAsync(breachEvent, cancellationToken);

                        var alert = new GeofenceAlertDto
                        {
                            BreachId = breachEvent.Id,
                            UserId = breadcrumb.UserId,
                            GeofenceId = geofence.Id,
                            GeofenceName = geofence.Name,
                            LocationRef = geofence.LocationRef,
                            BreachType = "EXIT",
                            Latitude = breadcrumb.Latitude,
                            Longitude = breadcrumb.Longitude,
                            AccuracyMeters = breadcrumb.AccuracyMeters,
                            DistanceFromBoundaryMeters = distanceFromBoundary,
                            SpeedKmh = breadcrumb.SpeedKmh,
                            Severity = "HIGH",
                            Message = $"Employee {breadcrumb.UserId} is outside assigned geofence '{geofence.Name}' ({geofence.LocationRef}).",
                            TriggeredAt = breadcrumb.RecordedAt
                        };

                        alerts.Add(alert);
                    }
                }
                else if (previousState == StateInside && !isInside)
                {
                    // TRANSITION: Inside -> Outside (EXIT Breach)
                    await _stateCache.SetStateAsync(breadcrumb.UserId, geofence.Id, StateOutside, TimeSpan.FromHours(12), cancellationToken);

                    var breachEvent = new GeofenceBreachEvent(
                        breadcrumb.UserId,
                        geofence.Id,
                        geofence.Name,
                        geofence.LocationRef,
                        "EXIT",
                        breadcrumb.Latitude,
                        breadcrumb.Longitude,
                        breadcrumb.AccuracyMeters,
                        distanceFromBoundary,
                        breadcrumb.SpeedKmh,
                        breadcrumb.RecordedAt);

                    await _breachRepository.AddAsync(breachEvent, cancellationToken);

                    var alert = new GeofenceAlertDto
                    {
                        BreachId = breachEvent.Id,
                        UserId = breadcrumb.UserId,
                        GeofenceId = geofence.Id,
                        GeofenceName = geofence.Name,
                        LocationRef = geofence.LocationRef,
                        BreachType = "EXIT",
                        Latitude = breadcrumb.Latitude,
                        Longitude = breadcrumb.Longitude,
                        AccuracyMeters = breadcrumb.AccuracyMeters,
                        DistanceFromBoundaryMeters = distanceFromBoundary,
                        SpeedKmh = breadcrumb.SpeedKmh,
                        Severity = "HIGH",
                        Message = $"Employee {breadcrumb.UserId} exited assigned geofence '{geofence.Name}' ({geofence.LocationRef}).",
                        TriggeredAt = breadcrumb.RecordedAt
                    };

                    alerts.Add(alert);
                }
                else if (previousState == StateOutside && isInside)
                {
                    // TRANSITION: Outside -> Inside (ENTRY Event / Returned)
                    await _stateCache.SetStateAsync(breadcrumb.UserId, geofence.Id, StateInside, TimeSpan.FromHours(12), cancellationToken);

                    var breachEvent = new GeofenceBreachEvent(
                        breadcrumb.UserId,
                        geofence.Id,
                        geofence.Name,
                        geofence.LocationRef,
                        "ENTRY",
                        breadcrumb.Latitude,
                        breadcrumb.Longitude,
                        breadcrumb.AccuracyMeters,
                        0.0,
                        breadcrumb.SpeedKmh,
                        breadcrumb.RecordedAt);

                    await _breachRepository.AddAsync(breachEvent, cancellationToken);

                    var alert = new GeofenceAlertDto
                    {
                        BreachId = breachEvent.Id,
                        UserId = breadcrumb.UserId,
                        GeofenceId = geofence.Id,
                        GeofenceName = geofence.Name,
                        LocationRef = geofence.LocationRef,
                        BreachType = "ENTRY",
                        Latitude = breadcrumb.Latitude,
                        Longitude = breadcrumb.Longitude,
                        AccuracyMeters = breadcrumb.AccuracyMeters,
                        DistanceFromBoundaryMeters = 0.0,
                        SpeedKmh = breadcrumb.SpeedKmh,
                        Severity = "INFO",
                        Message = $"Employee {breadcrumb.UserId} entered assigned geofence '{geofence.Name}' ({geofence.LocationRef}).",
                        TriggeredAt = breadcrumb.RecordedAt
                    };

                    alerts.Add(alert);
                }
                // When previousState is unchanged (e.g. OUTSIDE -> OUTSIDE or INSIDE -> INSIDE), do not generate duplicate spam alerts
            }

            if (alerts.Any())
            {
                await _breachRepository.SaveChangesAsync(cancellationToken);

                // Publish alerts real-time
                if (_streamPublisher != null)
                {
                    foreach (var alert in alerts)
                    {
                        try
                        {
                            await _streamPublisher.PublishGeofenceAlertAsync(alert, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to broadcast geofence alert for User {UserId}, Geofence {GeofenceId}", alert.UserId, alert.GeofenceId);
                        }
                    }
                }
            }

            return alerts;
        }
    }
}
