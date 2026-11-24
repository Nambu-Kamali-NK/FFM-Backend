using System.Collections.Generic;

namespace FFM.FieldOperations.Application.DTOs
{
    public class ScreenWidgetDto
    {
        public string WidgetId { get; set; } = string.Empty;
        public string WidgetType { get; set; } = string.Empty; // HERO_HEADER, CLIENT_SITE_CARD, FORM_SECTION, TELEMETRY_GAUGES, EVIDENCE_GALLERY, BOTTOM_ACTION_BAR
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public int Order { get; set; }
        public bool IsVisible { get; set; } = true;
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class ScreenThemeDto
    {
        public string PrimaryColor { get; set; } = "#1E3A8A";
        public string AccentColor { get; set; } = "#2563EB";
        public string BackgroundColor { get; set; } = "#F8FAFC";
        public string CardBackgroundColor { get; set; } = "#FFFFFF";
    }

    public class ScreenLayoutDto
    {
        public string ScreenId { get; set; } = "TASK_EXECUTION_SCREEN";
        public string Title { get; set; } = "Work Order Execution";
        public string Version { get; set; } = "2.0";
        public int? TaskId { get; set; }
        public string? TaskNumber { get; set; }
        public string TaskType { get; set; } = "GENERAL";
        public string? CurrentStatus { get; set; }
        public ScreenThemeDto Theme { get; set; } = new();
        public List<ScreenWidgetDto> Widgets { get; set; } = new();
    }
}

