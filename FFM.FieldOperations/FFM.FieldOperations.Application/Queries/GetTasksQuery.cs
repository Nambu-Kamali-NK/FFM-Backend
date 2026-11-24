using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Queries
{
    public class GetTasksQuery : IRequest<PagedResult<TaskDto>>
    {
        public GetTasksFilterQuery Filter { get; set; } = new();
        public int CallerUserId { get; set; }
        public IEnumerable<string> CallerRoles { get; set; } = new List<string>();
    }
}
