using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class DecidePermissionRequestCommand : IRequest<PermissionRequestDto>
    {
        public int RequestId { get; set; }
        public int ActorUserId { get; set; }
        public string Decision { get; set; } = string.Empty;
        public string? Comments { get; set; }
    }
}
