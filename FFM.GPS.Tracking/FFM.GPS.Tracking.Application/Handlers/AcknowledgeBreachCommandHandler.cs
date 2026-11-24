using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.Commands;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Interfaces;
using MediatR;

namespace FFM.GPS.Tracking.Application.Handlers
{
    public class AcknowledgeBreachCommandHandler : IRequestHandler<AcknowledgeBreachCommand, GeofenceBreachResponseDto>
    {
        private readonly IGeofenceBreachRepository _breachRepository;

        public AcknowledgeBreachCommandHandler(IGeofenceBreachRepository breachRepository)
        {
            _breachRepository = breachRepository;
        }

        public async Task<GeofenceBreachResponseDto> Handle(AcknowledgeBreachCommand request, CancellationToken cancellationToken)
        {
            if (request.BreachId <= 0)
                throw new ArgumentException("Invalid Breach ID.");

            var breach = await _breachRepository.GetByIdAsync(request.BreachId, cancellationToken);
            if (breach == null)
                throw new KeyNotFoundException($"Geofence breach record with ID {request.BreachId} was not found.");

            breach.Acknowledge(request.AcknowledgedBy, request.Notes);
            await _breachRepository.SaveChangesAsync(cancellationToken);

            return new GeofenceBreachResponseDto
            {
                Id = breach.Id,
                UserId = breach.UserId,
                GeofenceId = breach.GeofenceId,
                GeofenceName = breach.GeofenceName,
                LocationRef = breach.LocationRef,
                BreachType = breach.BreachType,
                Latitude = breach.Latitude,
                Longitude = breach.Longitude,
                AccuracyMeters = breach.AccuracyMeters,
                DistanceFromBoundaryMeters = breach.DistanceFromBoundaryMeters,
                SpeedKmh = breach.SpeedKmh,
                TriggeredAt = breach.TriggeredAt,
                IsAcknowledged = breach.IsAcknowledged,
                AcknowledgedBy = breach.AcknowledgedBy,
                AcknowledgedAt = breach.AcknowledgedAt,
                Notes = breach.Notes,
                CreatedAt = breach.CreatedAt
            };
        }
    }
}
