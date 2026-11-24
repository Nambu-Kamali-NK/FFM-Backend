using System;

namespace FFM.Workforce.Domain.Entities;

public class Shift
{
    public int Id { get; private set; }
    public string ShiftCode { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public int GracePeriodMinutes { get; private set; } = 15;
    public bool IsNightShift { get; private set; } = false;
    public bool IsRestDay { get; private set; } = false;
    public string ColorCode { get; private set; } = "#1C585C";
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string CreatedBy { get; private set; } = "SYSTEM";

    // EF Core constructor
    private Shift() { }

    public Shift(
        int id,
        string shiftCode,
        string name,
        string? description,
        TimeSpan startTime,
        TimeSpan endTime,
        int gracePeriodMinutes,
        bool isNightShift,
        bool isRestDay,
        string colorCode,
        bool isActive = true,
        string createdBy = "SYSTEM")
    {
        Id = id;
        ShiftCode = shiftCode ?? throw new ArgumentNullException(nameof(shiftCode));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        StartTime = startTime;
        EndTime = endTime;
        GracePeriodMinutes = gracePeriodMinutes;
        IsNightShift = isNightShift;
        IsRestDay = isRestDay;
        ColorCode = colorCode ?? "#1C585C";
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    public double CalculateDurationHours()
    {
        if (IsRestDay) return 0.0;
        if (EndTime >= StartTime)
        {
            return (EndTime - StartTime).TotalHours;
        }
        // Overnight crossover (e.g. 23:00 to 07:00)
        return (TimeSpan.FromHours(24) - StartTime + EndTime).TotalHours;
    }
}
