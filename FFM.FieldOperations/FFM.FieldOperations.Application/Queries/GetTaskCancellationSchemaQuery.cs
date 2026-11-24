using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetTaskCancellationSchemaQuery : IRequest<TaskCancellationSchemaDto>
    {
    }
}

