using System;

namespace FFM.Workforce.Domain.Entities;

public enum RosterAssignmentStatus
{
    ASSIGNED = 1,
    CONFIRMED = 2,
    COMPLETED = 3,
    CANCELLED = 4
}

public class RosterAssignment
{
    public int Id { get; private set; }
    public int EmployeeId { get; private set; }
    public int ShiftId { get; private set; }
    public DateTime RosterDate { get; private set; }
    public string? Zone { get; private set; }
    public string? Notes { get; private set; }
    public RosterAssignmentStatus Status { get; private set; } = RosterAssignmentStatus.ASSIGNED;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string CreatedBy { get; private set; } = "SYSTEM";

    // Navigation properties
    public Employee? Employee { get; private set; }
    public Shift? Shift { get; private set; }

    // EF Core constructor
    private RosterAssignment() { }

    public RosterAssignment(
        int employeeId,
        int shiftId,
        DateTime rosterDate,
        string? zone,
        string? notes,
        string createdBy = "SYSTEM",
        RosterAssignmentStatus status = RosterAssignmentStatus.ASSIGNED)
    {
        if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId), "EmployeeId must be positive.");
        if (shiftId <= 0) throw new ArgumentOutOfRangeException(nameof(shiftId), "ShiftId must be positive.");

        EmployeeId = employeeId;
        ShiftId = shiftId;
        RosterDate = rosterDate.Date;
        Zone = zone?.Trim();
        Notes = notes?.Trim();
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "SYSTEM" : createdBy.Trim();
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public void Cancel(string updatedBy)
    {
        Status = RosterAssignmentStatus.CANCELLED;
    }

    public void UpdateShift(int shiftId, string? zone, string? notes, string updatedBy)
    {
        if (shiftId <= 0) throw new ArgumentOutOfRangeException(nameof(shiftId), "ShiftId must be positive.");
        ShiftId = shiftId;
        Zone = zone?.Trim();
        Notes = notes?.Trim();
    }

    public void UpdateZoneAndNotes(string? zone, string? notes)
    {
        Zone = zone?.Trim();
        Notes = notes?.Trim();
    }
}
