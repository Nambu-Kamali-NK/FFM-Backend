using System;

namespace FFM.Workforce.Application.DTOs;

public class AssignRosterRequest
{
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateTime RosterDate { get; set; }
    public string? Zone { get; set; }
    public string? Notes { get; set; }
}
