using System;
using FFM.GPS.Tracking.Application.DTOs;
using MediatR;

namespace FFM.GPS.Tracking.Application.Queries
{
    public class GetGeofenceBreachesQuery : IRequest<PagedBreachesResultDto>
    {
        public int? UserId { get; set; }
        public int? GeofenceId { get; set; }
        public string? BreachType { get; set; }
        public bool? IsAcknowledged { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
