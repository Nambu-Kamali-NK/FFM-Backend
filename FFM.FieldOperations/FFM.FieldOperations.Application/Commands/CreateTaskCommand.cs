using System;
using System.Collections.Generic;
using FFM.FieldOperations.Application.DTOs;
using MediatR;

namespace FFM.FieldOperations.Application.Commands
{
    public class CreateTaskCommand : IRequest<TaskDto>
    {
        public int ActorUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Priority { get; set; } = "MEDIUM";
        public int ClientSiteId { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public string? TaskType { get; set; } = "GENERAL";
        public Dictionary<string, object>? CustomData { get; set; }
    }
}
