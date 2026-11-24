using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetTaskActionsQuery : IRequest<TaskActionsDto>
    {
        public int TaskId { get; }

        public GetTaskActionsQuery(int taskId)
        {
            TaskId = taskId;
        }
    }
}

