using System;

namespace FFM.Workforce.Domain.Entities
{
    public class LeaveRequest
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public int LeaveTypeId { get; private set; }
        public LeaveType LeaveType { get; private set; } = default!;
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }
        public decimal DaysCount { get; private set; }
        public bool IsHalfDay { get; private set; }
        public string Reason { get; private set; } = default!;
        public LeaveStatus Status { get; private set; }
        public int? ApproverId { get; private set; }
        public string? DecisionComments { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private LeaveRequest() { }

        public LeaveRequest(
            int employeeId,
            int leaveTypeId,
            DateTime startDate,
            DateTime endDate,
            decimal daysCount,
            bool isHalfDay,
            string reason)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Invalid Employee ID.", nameof(employeeId));
            if (leaveTypeId <= 0)
                throw new ArgumentException("Invalid Leave Type ID.", nameof(leaveTypeId));
            if (endDate < startDate)
                throw new ArgumentException("EndDate cannot be earlier than StartDate.", nameof(endDate));
            if (daysCount <= 0)
                throw new ArgumentException("DaysCount must be greater than zero.", nameof(daysCount));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason is required.", nameof(reason));

            EmployeeId = employeeId;
            LeaveTypeId = leaveTypeId;
            StartDate = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
            EndDate = DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc);
            DaysCount = daysCount;
            IsHalfDay = isHalfDay;
            Reason = reason.Trim();
            Status = LeaveStatus.PENDING;
            CreatedAt = DateTime.UtcNow;
        }

        public void Approve(int approverId, string? comments = null)
        {
            if (Status != LeaveStatus.PENDING)
                throw new InvalidOperationException($"Cannot approve leave request with status '{Status}'. Only PENDING requests can be approved.");

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
                throw new InvalidOperationException($"Cannot reject leave request with status '{Status}'. Only PENDING requests can be rejected.");

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

            if (Status == LeaveStatus.APPROVED && DateTime.UtcNow.Date >= StartDate.Date)
                throw new InvalidOperationException("Cannot cancel an approved leave that has already started or passed.");

            Status = LeaveStatus.CANCELLED;
        }
    }
}
