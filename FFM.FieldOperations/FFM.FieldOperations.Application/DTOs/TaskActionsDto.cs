using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class TaskActionItemDto
    {
        public string ActionKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Style { get; set; } = "PRIMARY"; // PRIMARY, SUCCESS, WARNING, DANGER, SECONDARY
        public string Icon { get; set; } = "touch_app";
        public string TargetEndpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = "POST";
        public bool RequiresConfirmation { get; set; } = false;
        public string? ConfirmationTitle { get; set; }
        public string? ConfirmationMessage { get; set; }
        public Dictionary<string, object>? Payload { get; set; }
    }

    public class TaskActionsDto
    {
        public int TaskId { get; set; }
        public string TaskNumber { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public string TaskType { get; set; } = "GENERAL";
        public List<TaskActionItemDto> Actions { get; set; } = new List<TaskActionItemDto>();
    }
}

