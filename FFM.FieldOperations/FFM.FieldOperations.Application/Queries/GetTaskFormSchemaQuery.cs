using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetTaskFormSchemaQuery : IRequest<TaskFormSchemaDto>
    {
        public string TaskType { get; set; } = "GENERAL";

        public GetTaskFormSchemaQuery(string? taskType = null)
        {
            TaskType = string.IsNullOrWhiteSpace(taskType) ? "GENERAL" : taskType.Trim().ToUpperInvariant();
        }
    }
}

