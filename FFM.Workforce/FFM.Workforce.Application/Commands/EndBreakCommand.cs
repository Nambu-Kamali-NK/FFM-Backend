using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class EndBreakCommand : IRequest<EndBreakResponseDto>
    {
        public int ActorUserId { get; set; }
        public string? Notes { get; set; }
    }
}
