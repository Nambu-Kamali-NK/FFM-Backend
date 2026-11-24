using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Application.Queries;
using FFM.GPS.Tracking.Domain.Interfaces;
using MediatR;

namespace FFM.GPS.Tracking.Application.Handlers
{
    public class GetGeofenceBreachesQueryHandler : IRequestHandler<GetGeofenceBreachesQuery, PagedBreachesResultDto>
    {
        private readonly IGeofenceBreachRepository _breachRepository;

        public GetGeofenceBreachesQueryHandler(IGeofenceBreachRepository breachRepository)
        {
            _breachRepository = breachRepository;
        }

        public async Task<PagedBreachesResultDto> Handle(GetGeofenceBreachesQuery request, CancellationToken cancellationToken)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var totalCount = await _breachRepository.GetBreachesCountAsync(
                request.UserId,
                request.GeofenceId,
                request.BreachType,
                request.IsAcknowledged,
                request.FromDate,
                request.ToDate,
                cancellationToken);

            var breaches = await _breachRepository.GetBreachesAsync(
                request.UserId,
                request.GeofenceId,
                request.BreachType,
                request.IsAcknowledged,
                request.FromDate,
                request.ToDate,
                page,
                pageSize,
                cancellationToken);

            var items = breaches.Select(b => new GeofenceBreachResponseDto
            {
                Id = b.Id,
                UserId = b.UserId,
                GeofenceId = b.GeofenceId,
                GeofenceName = b.GeofenceName,
                LocationRef = b.LocationRef,
                BreachType = b.BreachType,
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                AccuracyMeters = b.AccuracyMeters,
                DistanceFromBoundaryMeters = b.DistanceFromBoundaryMeters,
                SpeedKmh = b.SpeedKmh,
                TriggeredAt = b.TriggeredAt,
                IsAcknowledged = b.IsAcknowledged,
                AcknowledgedBy = b.AcknowledgedBy,
                AcknowledgedAt = b.AcknowledgedAt,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt
            }).ToList();

            return new PagedBreachesResultDto
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = items
            };
        }
    }
}
