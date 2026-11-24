using System;

namespace FFM.Workforce.Domain.Entities
{
    public class AttendanceAuditLog
    {
        public int Id { get; private set; }
        public int AttendanceId { get; private set; }
        public int ActorUserId { get; private set; }
        public string Action { get; private set; } = string.Empty;
        public string PreviousValues { get; private set; } = string.Empty;
        public string NewValues { get; private set; } = string.Empty;
        public string Reason { get; private set; } = string.Empty;
        public DateTime Timestamp { get; private set; }

        private AttendanceAuditLog() { }

        public AttendanceAuditLog(
            int attendanceId,
            int actorUserId,
            string action,
            string previousValues,
            string newValues,
            string reason)
        {
            if (attendanceId <= 0)
                throw new ArgumentException("Invalid Attendance ID.", nameof(attendanceId));

            if (actorUserId <= 0)
                throw new ArgumentException("Invalid Actor User ID.", nameof(actorUserId));

            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action is required.", nameof(action));

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason is required.", nameof(reason));

            AttendanceId = attendanceId;
            ActorUserId = actorUserId;
            Action = action;
            PreviousValues = previousValues ?? string.Empty;
            NewValues = newValues ?? string.Empty;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }
}
