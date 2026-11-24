using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Interfaces;
using FFM.GPS.Tracking.Domain.Services;
using MediatR;

namespace FFM.GPS.Tracking.Application.Handlers
{
    public class ValidateGeofenceCommandHandler : IRequestHandler<ValidateGeofenceCommand, ValidateGeofenceResponseDto>
    {
        private readonly IGeofenceRepository _geofenceRepository;
        private readonly IGeofenceValidationService _validationService;

        public ValidateGeofenceCommandHandler(
            IGeofenceRepository geofenceRepository,
            IGeofenceValidationService validationService)
        {
            _geofenceRepository = geofenceRepository;
            _validationService = validationService;
        }

        public async Task<ValidateGeofenceResponseDto> Handle(ValidateGeofenceCommand request, CancellationToken cancellationToken)
        {
            // Fetch assigned geofences for employee; fallback to all active geofences if no employee-specific mapping exists
            var assignedGeofences = (await _geofenceRepository.GetAssignedGeofencesByEmployeeIdAsync(request.EmployeeId)).ToList();
            if (!assignedGeofences.Any())
            {
                assignedGeofences = (await _geofenceRepository.GetAllActiveAsync()).ToList();
            }

            if (!assignedGeofences.Any())
            {
                // No active geofences configured in system
                return new ValidateGeofenceResponseDto
                {
                    IsValidGeofence = true,
                    Message = "No active geofence boundaries configured."
                };
            }

            foreach (var geofence in assignedGeofences)
            {
                if (!geofence.IsActive)
                    continue;

                if (geofence.Type.Equals("CIRCULAR", StringComparison.OrdinalIgnoreCase))
                {
                    if (geofence.CenterLatitude.HasValue && geofence.CenterLongitude.HasValue && geofence.RadiusMeters.HasValue)
                    {
                        var isInside = _validationService.IsPointInCircularGeofence(
                            request.Latitude,
                            request.Longitude,
                            geofence.CenterLatitude.Value,
                            geofence.CenterLongitude.Value,
                            geofence.RadiusMeters.Value,
                            geofence.BufferMeters);

                        if (isInside)
                        {
                            var distance = _validationService.CalculateHaversineDistanceMeters(
                                request.Latitude, request.Longitude,
                                geofence.CenterLatitude.Value, geofence.CenterLongitude.Value);

                            return new ValidateGeofenceResponseDto
                            {
                                IsValidGeofence = true,
                                LocationRef = geofence.LocationRef,
                                SiteName = geofence.Name,
                                DistanceFromCenterMeters = Math.Round(distance, 2),
                                Message = "Location verified inside authorized geofence."
                            };
                        }
                    }
                }
                else if (geofence.Type.Equals("POLYGON", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(geofence.PolygonCoordinatesJson))
                    {
                        try
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var vertices = JsonSerializer.Deserialize<FFM.GPS.Tracking.Domain.Entities.CoordinateVertex[]>(geofence.PolygonCoordinatesJson, options);
                            if (vertices != null && _validationService.IsPointInPolygonGeofence(request.Latitude, request.Longitude, vertices))
                            {
                                return new ValidateGeofenceResponseDto
                                {
                                    IsValidGeofence = true,
                                    LocationRef = geofence.LocationRef,
                                    SiteName = geofence.Name,
                                    DistanceFromCenterMeters = 0.0,
                                    Message = "Location verified inside authorized polygon geofence."
                                };
                            }
                        }
                        catch
                        {
                            // Ignore invalid JSON polygon format and continue
                        }
                    }
                }
            }

            return new ValidateGeofenceResponseDto
            {
                IsValidGeofence = false,
                LocationRef = null,
                SiteName = null,
                DistanceFromCenterMeters = null,
                Message = "User location is outside all assigned geofence boundaries."
            };
        }
    }
}
