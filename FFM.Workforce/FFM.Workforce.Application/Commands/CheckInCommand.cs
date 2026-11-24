using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class CheckInCommand : IRequest<AttendanceCheckInResponseDto>
    {
        public int ActorUserId { get; set; }
        public string? CheckInNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
