using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class TaskFormFieldDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Widget { get; set; } = "TEXT_INPUT"; // TEXT_INPUT, TEXT_AREA, NUMBER_INPUT, DROPDOWN, TOGGLE_SWITCH, DATE_TIME_PICKER, SEARCHABLE_DROPDOWN
        public bool Required { get; set; }
        public string? Placeholder { get; set; }
        public object? DefaultValue { get; set; }
        public List<string>? Options { get; set; }
        public string? ValidationRegex { get; set; }
    }

    public class TaskFormSectionDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<TaskFormFieldDto> Fields { get; set; } = new();
    }

    public class TaskFormSchemaDto
    {
        public string TaskType { get; set; } = "GENERAL";
        public string FormTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<TaskFormSectionDto> Sections { get; set; } = new();
    }
}

