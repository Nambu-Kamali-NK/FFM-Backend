using System;
using System.Collections.Generic;
using System.Linq;

namespace FFM.Workforce.Domain.Entities
{
    public class AttendanceRecord
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public DateTime WorkDate { get; private set; }
        public DateTime CheckInTime { get; private set; }
        public DateTime? CheckOutTime { get; private set; }
        public AttendanceStatus Status { get; private set; }
        public double TotalWorkingHours { get; private set; }
        public int TotalBreakMinutes { get; private set; }
        public string? CheckInNotes { get; private set; }
        public string? CheckOutNotes { get; private set; }
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }
        public bool IsGeofenceValidated { get; private set; }
        public string? LocationRef { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private AttendanceRecord() { }

        public AttendanceRecord(
            int employeeId,
            DateTime checkInTime,
            string? checkInNotes = null,
            double? latitude = null,
            double? longitude = null)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Invalid Employee ID.", nameof(employeeId));

            EmployeeId = employeeId;
            CheckInTime = checkInTime;
            WorkDate = DateTime.SpecifyKind(checkInTime.Date, DateTimeKind.Utc);
            Status = AttendanceStatus.PRESENT;
            CheckInNotes = checkInNotes;
            Latitude = latitude;
            Longitude = longitude;
            TotalWorkingHours = 0.0;
            TotalBreakMinutes = 0;
            CreatedAt = DateTime.UtcNow;
        }

        public AttendanceRecord(
            int id,
            int employeeId,
            DateTime workDate,
            DateTime checkInTime,
            DateTime? checkOutTime,
            AttendanceStatus status,
            double totalWorkingHours,
            int totalBreakMinutes,
            string? checkInNotes,
            string? checkOutNotes,
            DateTime createdAt,
            double? latitude = null,
            double? longitude = null)
        {
            Id = id;
            EmployeeId = employeeId;
            WorkDate = workDate.Date;
            CheckInTime = checkInTime;
            CheckOutTime = checkOutTime;
            Status = status;
            TotalWorkingHours = totalWorkingHours;
            TotalBreakMinutes = totalBreakMinutes;
            CheckInNotes = checkInNotes;
            CheckOutNotes = checkOutNotes;
            CreatedAt = createdAt;
            Latitude = latitude;
            Longitude = longitude;
        }

        public ICollection<BreakRecord> Breaks { get; private set; } = new List<BreakRecord>();

        public BreakRecord StartBreak(string breakType, DateTime breakStartTime)
        {
            if (Status == AttendanceStatus.COMPLETED)
                throw new InvalidOperationException("Cannot start a break on a completed attendance record.");

            if (Status == AttendanceStatus.ON_BREAK)
                throw new InvalidOperationException("Cannot start a new break while a break is already open.");

            var breakRecord = new BreakRecord(Id, breakType, breakStartTime);
            Breaks.Add(breakRecord);
            Status = AttendanceStatus.ON_BREAK;
            UpdatedAt = DateTime.UtcNow;

            return breakRecord;
        }

        public BreakRecord EndBreak(DateTime breakEndTime)
        {
            if (Status != AttendanceStatus.ON_BREAK)
                throw new InvalidOperationException("Employee is not currently on an active break.");

            var activeBreak = Breaks.FirstOrDefault(b => !b.BreakEndTime.HasValue);
            if (activeBreak == null)
                throw new InvalidOperationException("No open break record found to end.");

            activeBreak.EndBreak(breakEndTime);
            TotalBreakMinutes += activeBreak.DurationMinutes ?? 0;
            Status = AttendanceStatus.PRESENT;
            UpdatedAt = DateTime.UtcNow;

            return activeBreak;
        }

        public void CheckOut(DateTime checkOutTime, string? checkOutNotes = null)
        {
            if (Status == AttendanceStatus.COMPLETED)
                throw new InvalidOperationException("Attendance record is already checked out.");

            if (Status == AttendanceStatus.ON_BREAK)
                throw new InvalidOperationException("Cannot check out while on an active break. Please end break first.");

            if (checkOutTime < CheckInTime)
                throw new InvalidOperationException("Check-out time cannot be earlier than check-in time.");

            CheckOutTime = checkOutTime;
            CheckOutNotes = checkOutNotes;
            Status = AttendanceStatus.COMPLETED;

            double rawHours = (checkOutTime - CheckInTime).TotalHours;
            double netHours = rawHours - (TotalBreakMinutes / 60.0);
            TotalWorkingHours = Math.Max(0.0, Math.Round(netHours, 2));
            UpdatedAt = DateTime.UtcNow;
        }

        public void Correct(DateTime? newCheckInTime, DateTime? newCheckOutTime, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Correction reason is required.", nameof(reason));

            DateTime effectiveCheckIn = newCheckInTime.HasValue
                ? (newCheckInTime.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(newCheckInTime.Value, DateTimeKind.Utc) : newCheckInTime.Value.ToUniversalTime())
                : CheckInTime;

            DateTime? effectiveCheckOut = newCheckOutTime.HasValue
                ? (newCheckOutTime.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(newCheckOutTime.Value, DateTimeKind.Utc) : newCheckOutTime.Value.ToUniversalTime())
                : CheckOutTime;

            if (effectiveCheckOut.HasValue && effectiveCheckOut.Value < effectiveCheckIn)
                throw new InvalidOperationException("Check-out time cannot be earlier than check-in time.");

            CheckInTime = effectiveCheckIn;
            var newWorkDate = DateTime.SpecifyKind(effectiveCheckIn.Date, DateTimeKind.Utc);
            if (WorkDate != newWorkDate)
            {
                WorkDate = newWorkDate;
            }
            CheckOutTime = effectiveCheckOut;
            Status = AttendanceStatus.CORRECTED;

            if (effectiveCheckOut.HasValue)
            {
                double rawHours = (effectiveCheckOut.Value - effectiveCheckIn).TotalHours;
                double netHours = rawHours - (TotalBreakMinutes / 60.0);
                TotalWorkingHours = Math.Max(0.0, Math.Round(netHours, 2));
            }

            UpdatedAt = DateTime.UtcNow;
        }
    }
}
