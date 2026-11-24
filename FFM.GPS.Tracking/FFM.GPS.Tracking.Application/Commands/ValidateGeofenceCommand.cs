using FFM.GPS.Tracking.Application.DTOs;
using MediatR;

namespace FFM.GPS.Tracking.Application.Commands
{
    public class ValidateGeofenceCommand : IRequest<ValidateGeofenceResponseDto>
    {
        public int EmployeeId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double GpsAccuracyMeters { get; set; } = 5.0;
    }
}
