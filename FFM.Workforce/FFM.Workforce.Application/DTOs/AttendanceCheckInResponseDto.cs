using System;

namespace FFM.Workforce.Application.DTOs
{
    public class AttendanceCheckInResponseDto
    {
        public int AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public string WorkDate { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CheckInNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
