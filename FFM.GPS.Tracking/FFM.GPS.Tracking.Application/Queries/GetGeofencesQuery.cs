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

namespace FFM.GPS.Tracking.Application.Queries
{
    public class GetGeofencesQuery : IRequest<List<GeofenceResponseDto>>
    {
    }

    public class GetGeofencesQueryHandler : IRequestHandler<GetGeofencesQuery, List<GeofenceResponseDto>>
    {
        private readonly IGeofenceRepository _geofenceRepository;

        public GetGeofencesQueryHandler(IGeofenceRepository geofenceRepository)
        {
            _geofenceRepository = geofenceRepository;
        }

        public async Task<List<GeofenceResponseDto>> Handle(GetGeofencesQuery request, CancellationToken cancellationToken)
        {
            var list = await _geofenceRepository.GetAllActiveAsync();
            return list.Select(g =>
            {
                var coords = new List<CoordinatePoint>();
                if (g.Type.Equals("CIRCULAR", StringComparison.OrdinalIgnoreCase) && g.CenterLatitude.HasValue && g.CenterLongitude.HasValue)
                {
                    coords.Add(new CoordinatePoint(g.CenterLatitude.Value, g.CenterLongitude.Value));
                }
                else if (g.Type.Equals("POLYGON", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(g.PolygonCoordinatesJson))
                {
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var vertices = JsonSerializer.Deserialize<CoordinateVertex[]>(g.PolygonCoordinatesJson, options);
                        if (vertices != null)
                        {
                            coords.AddRange(vertices.Select(v => new CoordinatePoint(v.Lat, v.Lon)));
                        }
                    }
                    catch
                    {
                        // Ignore malformed JSON and return empty coordinates
                    }
                }

                return new GeofenceResponseDto
                {
                    Id = g.Id,
                    LocationRef = g.LocationRef,
                    Name = g.Name,
                    Type = g.Type,
                    RadiusMeters = g.RadiusMeters,
                    Coordinates = coords,
                    IsActive = g.IsActive,
                    CreatedAt = g.CreatedAt
                };
            }).ToList();
        }
    }
}
