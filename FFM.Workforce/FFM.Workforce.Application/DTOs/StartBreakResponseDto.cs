using System;

namespace FFM.Workforce.Application.DTOs
{
    public class StartBreakResponseDto
    {
        public int BreakId { get; set; }
        public int AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public string BreakType { get; set; } = string.Empty;
        public DateTime BreakStartTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
