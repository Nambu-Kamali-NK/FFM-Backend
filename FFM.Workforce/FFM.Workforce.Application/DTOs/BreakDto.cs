using System;

namespace FFM.Workforce.Application.DTOs
{
    public class BreakDto
    {
        public int Id { get; set; }
        public string BreakType { get; set; } = string.Empty;
        public DateTime BreakStartTime { get; set; }
        public DateTime? BreakEndTime { get; set; }
        public int? DurationMinutes { get; set; }
    }
}
