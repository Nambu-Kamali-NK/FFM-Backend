using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class AssignTaskCommand : IRequest<TaskDto>
    {
        public int TaskId { get; set; }
        public int AssignedEmployeeId { get; set; }
        public int ActorUserId { get; set; }
    }
}
