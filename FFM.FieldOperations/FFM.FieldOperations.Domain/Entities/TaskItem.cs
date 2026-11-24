using System;

namespace FFM.FieldOperations.Domain.Entities
{
    public class TaskItem
    {
        public int Id { get; private set; }
        public string TaskNumber { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public string Priority { get; private set; } = "MEDIUM"; // LOW, MEDIUM, HIGH, URGENT
        public string Status { get; private set; } = "CREATED"; // CREATED, ASSIGNED, EN_ROUTE, CHECKED_IN, COMPLETED, CANCELLED
        public int ClientSiteId { get; private set; }
        public int? AssignedEmployeeId { get; private set; }
        public DateTime ScheduledStart { get; private set; }
        public DateTime ScheduledEnd { get; private set; }
        public int CreatedByUserId { get; private set; }
        public string TaskType { get; private set; } = "GENERAL";
        public string? CustomDataJson { get; private set; }
        public string? CancellationReason { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public ClientSite? ClientSite { get; private set; }

        private TaskItem() { }

        public TaskItem(
            string taskNumber,
            string title,
            string? description,
            string priority,
            int clientSiteId,
            int? assignedEmployeeId,
            DateTime scheduledStart,
            DateTime scheduledEnd,
            int createdByUserId,
            string? taskType = "GENERAL",
            string? customDataJson = null)
        {
            if (string.IsNullOrWhiteSpace(taskNumber))
                throw new ArgumentException("TaskNumber cannot be empty.", nameof(taskNumber));

            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 5)
                throw new ArgumentException("Title must be at least 5 characters.", nameof(title));

            if (title.Trim().Length > 200)
                throw new ArgumentException("Title cannot exceed 200 characters.", nameof(title));

            if (clientSiteId <= 0)
                throw new ArgumentException("Invalid ClientSiteId.", nameof(clientSiteId));

            var startUtc = scheduledStart.Kind == DateTimeKind.Utc ? scheduledStart : DateTime.SpecifyKind(scheduledStart, DateTimeKind.Utc);
            var endUtc = scheduledEnd.Kind == DateTimeKind.Utc ? scheduledEnd : DateTime.SpecifyKind(scheduledEnd, DateTimeKind.Utc);

            if (endUtc <= startUtc)
                throw new ArgumentException("ScheduledEnd must be strictly later than ScheduledStart.", nameof(scheduledEnd));

            var validPriorities = new[] { "LOW", "MEDIUM", "HIGH", "URGENT" };
            var normalizedPriority = string.IsNullOrWhiteSpace(priority) ? "MEDIUM" : priority.Trim().ToUpperInvariant();
            if (!Array.Exists(validPriorities, p => p == normalizedPriority))
                throw new ArgumentException($"Priority must be one of: LOW, MEDIUM, HIGH, URGENT. Received: '{priority}'", nameof(priority));

            TaskNumber = taskNumber.Trim();
            Title = title.Trim();
            Description = description?.Trim();
            Priority = normalizedPriority;
            ClientSiteId = clientSiteId;
            AssignedEmployeeId = (assignedEmployeeId.HasValue && assignedEmployeeId.Value > 0) ? assignedEmployeeId.Value : null;
            Status = AssignedEmployeeId.HasValue ? "ASSIGNED" : "CREATED";
            ScheduledStart = startUtc;
            ScheduledEnd = endUtc;
            CreatedByUserId = createdByUserId;
            TaskType = string.IsNullOrWhiteSpace(taskType) ? "GENERAL" : taskType.Trim().ToUpperInvariant();
            CustomDataJson = string.IsNullOrWhiteSpace(customDataJson) ? null : customDataJson.Trim();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateCustomData(string? taskType, string? customDataJson)
        {
            if (!string.IsNullOrWhiteSpace(taskType))
            {
                TaskType = taskType.Trim().ToUpperInvariant();
            }
            CustomDataJson = string.IsNullOrWhiteSpace(customDataJson) ? null : customDataJson.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        public void Assign(int employeeId)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Invalid Employee ID.", nameof(employeeId));

            if (Status == "CHECKED_IN" || Status == "COMPLETED" || Status == "CANCELLED")
                throw new InvalidOperationException($"Cannot reassign a task that is in '{Status}' status.");

            AssignedEmployeeId = employeeId;
            Status = "ASSIGNED";
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateStatus(string newStatus)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
                throw new ArgumentException("Status cannot be empty.", nameof(newStatus));

            var target = newStatus.Trim().ToUpperInvariant();
            if (Status == "CANCELLED" || Status == "COMPLETED")
                throw new InvalidOperationException($"Cannot transition status from terminal state '{Status}'.");

            Status = target;
            UpdatedAt = DateTime.UtcNow;
        }

        public void StartTravel()
        {
            if (Status == "EN_ROUTE")
                return; // Idempotent

            if (Status == "CREATED" || !AssignedEmployeeId.HasValue)
                throw new InvalidOperationException("Cannot start travel on an unassigned task. Task must be assigned prior to departure.");

            if (Status == "CHECKED_IN" || Status == "COMPLETED" || Status == "CANCELLED")
                throw new InvalidOperationException($"Cannot transition task to EN_ROUTE from current status '{Status}'. Valid transitions to EN_ROUTE are only permitted from ASSIGNED status.");

            if (Status != "ASSIGNED")
                throw new InvalidOperationException($"Cannot transition task to EN_ROUTE from status '{Status}'. Valid transitions are only permitted from ASSIGNED status.");

            Status = "EN_ROUTE";
            UpdatedAt = DateTime.UtcNow;
        }

        public void Cancel(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                throw new ArgumentException("Cancellation reason must be provided (at least 5 characters).", nameof(reason));

            if (Status == "CHECKED_IN" || Status == "COMPLETED" || Status == "CANCELLED")
                throw new InvalidOperationException($"Cannot cancel a task that is in '{Status}' status.");

            Status = "CANCELLED";
            CancellationReason = reason.Trim();
            CancelledAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void CheckIn()
        {
            if (Status == "CHECKED_IN")
                return; // Idempotent

            if (Status == "CREATED" || !AssignedEmployeeId.HasValue)
                throw new InvalidOperationException("Cannot check in to an unassigned task. Task must be assigned prior to check-in.");

            if (Status == "COMPLETED" || Status == "CANCELLED")
                throw new InvalidOperationException($"Cannot check in to a task that is in '{Status}' status.");

            if (Status != "ASSIGNED" && Status != "EN_ROUTE")
                throw new InvalidOperationException($"Cannot check in to task from status '{Status}'. Valid transitions are only permitted from ASSIGNED or EN_ROUTE status.");

            Status = "CHECKED_IN";
            UpdatedAt = DateTime.UtcNow;
        }

        public void Complete()
        {
            if (Status == "COMPLETED")
                return; // Idempotent

            if (Status == "CANCELLED")
                throw new InvalidOperationException($"Cannot transition task to COMPLETED from terminal status '{Status}'.");

            if (Status == "CREATED" || !AssignedEmployeeId.HasValue)
                throw new InvalidOperationException("Cannot complete an unassigned task. Task must be assigned prior to completion.");

            Status = "COMPLETED";
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
