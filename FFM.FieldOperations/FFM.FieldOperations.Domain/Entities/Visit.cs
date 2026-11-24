using System;
using System.Collections.Generic;

namespace FFM.FieldOperations.Domain.Entities
{
    public class Visit
    {
        public int Id { get; private set; }
        public int TaskId { get; private set; }
        public int EmployeeId { get; private set; }
        public DateTime CheckInTime { get; private set; }
        public decimal CheckInLatitude { get; private set; }
        public decimal CheckInLongitude { get; private set; }
        public decimal CheckInDistanceMeters { get; private set; }
        public bool IsWithinGeofence { get; private set; }
        public DateTime? CheckOutTime { get; private set; }
        public decimal? CheckOutLatitude { get; private set; }
        public decimal? CheckOutLongitude { get; private set; }
        public decimal? DurationMinutes { get; private set; }
        public string? CompletionNotes { get; private set; }
        public string? CustomerName { get; private set; }
        public string? CustomerSignatureUrl { get; private set; }
        public int? CustomerRating { get; private set; }
        public string Status { get; private set; } = "CHECKED_IN"; // CHECKED_IN, COMPLETED, ABORTED
        public DateTime CreatedAt { get; private set; }

        public TaskItem? Task { get; private set; }
        private readonly List<VisitEvidence> _evidences = new();
        public IReadOnlyCollection<VisitEvidence> Evidences => _evidences.AsReadOnly();

        private Visit() { }

        public Visit(
            int taskId,
            int employeeId,
            DateTime checkInTime,
            decimal checkInLatitude,
            decimal checkInLongitude,
            decimal checkInDistanceMeters,
            bool isWithinGeofence)
        {
            if (taskId <= 0)
                throw new ArgumentException("TaskId must be greater than zero.", nameof(taskId));

            if (employeeId <= 0)
                throw new ArgumentException("EmployeeId must be greater than zero.", nameof(employeeId));

            if (checkInLatitude < -90m || checkInLatitude > 90m)
                throw new ArgumentException("CheckInLatitude must be between -90 and 90.", nameof(checkInLatitude));

            if (checkInLongitude < -180m || checkInLongitude > 180m)
                throw new ArgumentException("CheckInLongitude must be between -180 and 180.", nameof(checkInLongitude));

            if (checkInDistanceMeters < 0)
                throw new ArgumentException("CheckInDistanceMeters cannot be negative.", nameof(checkInDistanceMeters));

            TaskId = taskId;
            EmployeeId = employeeId;
            CheckInTime = checkInTime.Kind == DateTimeKind.Utc ? checkInTime : DateTime.SpecifyKind(checkInTime, DateTimeKind.Utc);
            CheckInLatitude = checkInLatitude;
            CheckInLongitude = checkInLongitude;
            CheckInDistanceMeters = checkInDistanceMeters;
            IsWithinGeofence = isWithinGeofence;
            Status = "CHECKED_IN";
            CreatedAt = DateTime.UtcNow;
        }

        public void AddEvidence(VisitEvidence evidence)
        {
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));

            if (Status == "ABORTED" || Status == "CANCELLED")
                throw new InvalidOperationException($"Cannot attach evidence to a visit in '{Status}' status.");

            _evidences.Add(evidence);
        }

        public void Complete(
            DateTime checkOutTime,
            decimal? checkOutLatitude,
            decimal? checkOutLongitude,
            string? completionNotes,
            string? customerName,
            string? customerSignatureUrl,
            int? customerRating = null)
        {
            if (Status != "CHECKED_IN")
                throw new InvalidOperationException($"Cannot complete a visit in '{Status}' status. Must be in 'CHECKED_IN'.");

            if (_evidences.Count == 0)
                throw new InvalidOperationException("At least one proof of visit evidence is required to complete the visit (BR-FLD-05).");

            var checkOutUtc = checkOutTime.Kind == DateTimeKind.Utc ? checkOutTime : DateTime.SpecifyKind(checkOutTime, DateTimeKind.Utc);
            if (checkOutUtc < CheckInTime)
                throw new ArgumentException("CheckOutTime cannot be earlier than CheckInTime.", nameof(checkOutTime));

            if (customerRating.HasValue && (customerRating.Value < 1 || customerRating.Value > 5))
                throw new ArgumentException("CustomerRating must be an integer between 1 and 5.", nameof(customerRating));

            CheckOutTime = checkOutUtc;
            CheckOutLatitude = checkOutLatitude;
            CheckOutLongitude = checkOutLongitude;
            CompletionNotes = completionNotes?.Trim();
            CustomerName = customerName?.Trim();
            CustomerSignatureUrl = customerSignatureUrl?.Trim();
            CustomerRating = customerRating;

            var duration = (checkOutUtc - CheckInTime).TotalMinutes;
            DurationMinutes = Math.Round((decimal)duration, 2);
            Status = "COMPLETED";
        }
    }
}

