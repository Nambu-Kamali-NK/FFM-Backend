using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class CancelLeaveRequestCommand : IRequest<LeaveRequestDto>
    {
        public int RequestId { get; set; }
        public int ActorUserId { get; set; }
    }
}
