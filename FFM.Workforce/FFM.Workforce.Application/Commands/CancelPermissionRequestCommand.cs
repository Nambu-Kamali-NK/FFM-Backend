using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class CancelPermissionRequestCommand : IRequest<PermissionRequestDto>
    {
        public int RequestId { get; set; }
        public int ActorUserId { get; set; }
    }
}
