using System;
using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class UpdateTaskStatusCommand : IRequest<TaskDto>
    {
        public int TaskId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int ActorUserId { get; set; }
        public IEnumerable<string>? ActorRoles { get; set; }
    }
}
