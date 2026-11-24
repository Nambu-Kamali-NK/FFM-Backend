using System;

namespace FFM.Workforce.Domain.Entities
{
    public class PermissionRequest
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public DateTime PermissionDate { get; private set; }
        public TimeSpan StartTime { get; private set; }
        public TimeSpan EndTime { get; private set; }
        public decimal DurationHours { get; private set; }
        public string Reason { get; private set; } = default!;
        public LeaveStatus Status { get; private set; }
        public int? ApproverId { get; private set; }
        public string? DecisionComments { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private PermissionRequest() { }

        public PermissionRequest(
            int employeeId,
            DateTime permissionDate,
            TimeSpan startTime,
            TimeSpan endTime,
            decimal durationHours,
            string reason)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Invalid Employee ID.", nameof(employeeId));
            if (endTime <= startTime)
                throw new ArgumentException("EndTime must be later than StartTime.", nameof(endTime));
            if (durationHours < 0.5m || durationHours > 4.0m)
                throw new ArgumentException("Duration must be between 0.5 and 4.0 hours.", nameof(durationHours));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason is required.", nameof(reason));

            EmployeeId = employeeId;
            PermissionDate = DateTime.SpecifyKind(permissionDate.Date, DateTimeKind.Utc);
            StartTime = startTime;
            EndTime = endTime;
            DurationHours = durationHours;
            Reason = reason.Trim();
            Status = LeaveStatus.PENDING;
            CreatedAt = DateTime.UtcNow;
        }

        public void Approve(int approverId, string? comments = null)
        {
            if (Status != LeaveStatus.PENDING)
                throw new InvalidOperationException($"Cannot approve permission request with status '{Status}'. Only PENDING requests can be approved.");

            if (approverId <= 0)
                throw new ArgumentException("Invalid approver ID.", nameof(approverId));

            if (approverId == EmployeeId)
                throw new InvalidOperationException("Self-approval is strictly forbidden.");

            Status = LeaveStatus.APPROVED;
            ApproverId = approverId;
            DecisionComments = comments?.Trim();
            DecidedAt = DateTime.UtcNow;
        }

        public void Reject(int approverId, string? comments = null)
        {
            if (Status != LeaveStatus.PENDING)
                throw new InvalidOperationException($"Cannot reject permission request with status '{Status}'. Only PENDING requests can be rejected.");

            if (approverId <= 0)
                throw new ArgumentException("Invalid approver ID.", nameof(approverId));

            if (approverId == EmployeeId)
                throw new InvalidOperationException("Self-rejection is strictly forbidden.");

            Status = LeaveStatus.REJECTED;
            ApproverId = approverId;
            DecisionComments = comments?.Trim();
            DecidedAt = DateTime.UtcNow;
        }

        public void Cancel()
        {
            if (Status == LeaveStatus.CANCELLED || Status == LeaveStatus.REJECTED)
                throw new InvalidOperationException($"Cannot cancel request with status '{Status}'.");

            if (Status == LeaveStatus.APPROVED && DateTime.UtcNow.Date >= PermissionDate.Date)
                throw new InvalidOperationException("Cannot cancel an approved permission that has already started or passed.");

            Status = LeaveStatus.CANCELLED;
        }
    }
}
