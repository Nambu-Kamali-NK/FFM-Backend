using System;

namespace FFM.Workforce.Domain.Entities
{
    public class BreakRecord
    {
        public int Id { get; private set; }
        public int AttendanceId { get; private set; }
        public string BreakType { get; private set; } = "LUNCH";
        public DateTime BreakStartTime { get; private set; }
        public DateTime? BreakEndTime { get; private set; }
        public int? DurationMinutes { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private BreakRecord() { }

        public BreakRecord(
            int attendanceId,
            string breakType,
            DateTime breakStartTime)
        {
            if (attendanceId < 0)
                throw new ArgumentException("Invalid Attendance ID.", nameof(attendanceId));

            if (string.IsNullOrWhiteSpace(breakType))
                throw new ArgumentException("BreakType is required.", nameof(breakType));

            AttendanceId = attendanceId;
            BreakType = breakType.ToUpperInvariant();
            BreakStartTime = breakStartTime;
            CreatedAt = DateTime.UtcNow;
        }

        public void EndBreak(DateTime breakEndTime)
        {
            if (BreakEndTime.HasValue)
                throw new InvalidOperationException("Break is already ended.");

            if (breakEndTime < BreakStartTime)
                throw new InvalidOperationException("Break end time cannot be earlier than break start time.");

            BreakEndTime = breakEndTime;
            double duration = (breakEndTime - BreakStartTime).TotalMinutes;
            DurationMinutes = (int)Math.Max(0, Math.Round(duration));
        }
    }
}
