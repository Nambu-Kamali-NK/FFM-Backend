using System;
using System.Collections.Generic;

namespace FFM.Workforce.Application.DTOs
{
    public class TeamAttendanceDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public Guid OrgUnitId { get; set; }
        public string WorkDate { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public double TotalWorkingHours { get; set; }
        public int TotalBreakMinutes { get; set; }
        public List<BreakDto> Breaks { get; set; } = new();
    }
}
