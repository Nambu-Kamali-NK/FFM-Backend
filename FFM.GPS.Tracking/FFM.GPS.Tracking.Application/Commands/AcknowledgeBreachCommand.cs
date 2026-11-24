using System;
using FFM.GPS.Tracking.Application.DTOs;
using MediatR;

namespace FFM.GPS.Tracking.Application.Commands
{
    public class AcknowledgeBreachCommand : IRequest<GeofenceBreachResponseDto>
    {
        public long BreachId { get; set; }
        public string AcknowledgedBy { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
