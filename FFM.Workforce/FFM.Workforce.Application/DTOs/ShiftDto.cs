using System;

namespace FFM.Workforce.Application.DTOs;

public class ShiftDto
{
    public int Id { get; set; }
    public string ShiftCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string StartTime { get; set; } = default!;
    public string EndTime { get; set; } = default!;
    public double DurationHours { get; set; }
    public int GracePeriodMinutes { get; set; }
    public bool IsNightShift { get; set; }
    public bool IsRestDay { get; set; }
    public string ColorCode { get; set; } = "#1C585C";
    public bool IsActive { get; set; }
}
