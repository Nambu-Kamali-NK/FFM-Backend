using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class CancelTaskCommand : IRequest<TaskDto>
    {
        public int TaskId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int ActorUserId { get; set; }
    }
}
