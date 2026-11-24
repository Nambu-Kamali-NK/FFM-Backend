using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class CheckOutCommand : IRequest<AttendanceCheckOutResponseDto>
    {
        public int ActorUserId { get; set; }
        public string? CheckOutNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
