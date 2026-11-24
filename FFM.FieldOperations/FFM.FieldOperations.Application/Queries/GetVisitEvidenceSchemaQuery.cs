using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetVisitEvidenceSchemaQuery : IRequest<TaskEvidenceSchemaDto>
    {
        public string? TaskType { get; }
        public int? TaskId { get; }

        public GetVisitEvidenceSchemaQuery(string? taskType = null, int? taskId = null)
        {
            TaskType = taskType;
            TaskId = taskId;
        }
    }
}

