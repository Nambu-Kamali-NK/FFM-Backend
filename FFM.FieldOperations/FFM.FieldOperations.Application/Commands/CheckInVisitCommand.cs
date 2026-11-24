using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class CheckInVisitCommand : IRequest<VisitCheckInResultDto>
    {
        public int TaskId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? AccuracyMeters { get; set; }
        public bool? IsMockLocation { get; set; }

        public int ActorUserId { get; set; }
        public IReadOnlyList<string>? ActorRoles { get; set; }
    }
}

