using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using MediatR;

namespace FFM.GPS.Tracking.Application.Commands
{
    public class CreateGeofenceCommand : IRequest<GeofenceResponseDto>
    {
        public string LocationRef { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "CIRCULAR";
        public double? RadiusMeters { get; set; }
        public List<CoordinatePoint> Coordinates { get; set; } = new();
    }

    public class CreateGeofenceCommandHandler : IRequestHandler<CreateGeofenceCommand, GeofenceResponseDto>
    {
        private readonly IGeofenceRepository _geofenceRepository;

        public CreateGeofenceCommandHandler(IGeofenceRepository geofenceRepository)
        {
            _geofenceRepository = geofenceRepository;
        }

        public async Task<GeofenceResponseDto> Handle(CreateGeofenceCommand request, CancellationToken cancellationToken)
        {
            var existing = await _geofenceRepository.GetByLocationRefAsync(request.LocationRef);
            if (existing != null)
            {
                throw new InvalidOperationException($"Geofence with LocationRef '{request.LocationRef}' already exists.");
            }

            var type = (request.Type ?? "CIRCULAR").ToUpperInvariant();
            double? centerLat = null;
            double? centerLon = null;
            double? radius = null;
            string? polygonJson = null;

            if (type == "CIRCULAR")
            {
                if (request.Coordinates == null || request.Coordinates.Count < 1)
                {
                    throw new ArgumentException("Circular geofence requires at least 1 coordinate point for the center.");
                }

                if (!request.RadiusMeters.HasValue || request.RadiusMeters.Value <= 0)
                {
                    throw new ArgumentException("RadiusMeters must be greater than 0 for circular geofences.");
                }

                centerLat = request.Coordinates[0].Latitude;
                centerLon = request.Coordinates[0].Longitude;
                radius = request.RadiusMeters.Value;
            }
            else if (type == "POLYGON")
            {
                if (request.Coordinates == null || request.Coordinates.Count < 3)
                {
                    throw new ArgumentException("Polygon geofence requires at least 3 coordinate vertices.");
                }

                var vertices = request.Coordinates.Select(c => new CoordinateVertex(c.Latitude, c.Longitude)).ToList();
                polygonJson = JsonSerializer.Serialize(vertices);
            }
            else
            {
                throw new ArgumentException($"Unsupported geofence type: {request.Type}. Supported types are CIRCULAR and POLYGON.");
            }

            var geofence = new Geofence(
                request.LocationRef,
                request.Name,
                type,
                centerLat,
                centerLon,
                radius,
                0.0,
                polygonJson);

            await _geofenceRepository.AddGeofenceAsync(geofence);
            await _geofenceRepository.SaveChangesAsync();

            return new GeofenceResponseDto
            {
                Id = geofence.Id,
                LocationRef = geofence.LocationRef,
                Name = geofence.Name,
                Type = geofence.Type,
                RadiusMeters = geofence.RadiusMeters,
                Coordinates = request.Coordinates,
                IsActive = geofence.IsActive,
                CreatedAt = geofence.CreatedAt
            };
        }
    }
}
