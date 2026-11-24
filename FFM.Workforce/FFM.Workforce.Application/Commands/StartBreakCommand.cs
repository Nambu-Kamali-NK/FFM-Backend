using FFM.Workforce.Application.DTOs;
using MediatR;

namespace FFM.Workforce.Application.Commands
{
    public class StartBreakCommand : IRequest<StartBreakResponseDto>
    {
        public int ActorUserId { get; set; }
        public string BreakType { get; set; } = "LUNCH";
    }
}
