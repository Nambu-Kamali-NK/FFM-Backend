using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetTaskScreenLayoutQuery : IRequest<ScreenLayoutDto>
    {
        public int? TaskId { get; set; }
        public string? TaskType { get; set; }

        public GetTaskScreenLayoutQuery(int? taskId = null, string? taskType = null)
        {
            TaskId = taskId;
            TaskType = taskType;
        }
    }
}

