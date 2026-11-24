using System;

namespace FFM.Workforce.Application.DTOs
{
    public class CorrectAttendanceRequest
    {
        public int AttendanceId { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
