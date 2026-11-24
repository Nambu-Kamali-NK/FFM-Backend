using System;

namespace FFM.Workforce.Application.DTOs;

public class RosterAssignmentDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public int ShiftId { get; set; }
    public string? ShiftCode { get; set; }
    public string? ShiftName { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? ColorCode { get; set; }
    public DateTime RosterDate { get; set; }
    public string? Zone { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "ASSIGNED";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
