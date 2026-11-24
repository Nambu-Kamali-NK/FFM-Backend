using System;

namespace FFM.Workforce.Application.DTOs
{
    public class AttendanceCheckOutResponseDto
    {
        public int AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public string WorkDate { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public DateTime CheckOutTime { get; set; }
        public int TotalBreakMinutes { get; set; }
        public double NetWorkingHours { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CheckOutNotes { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
