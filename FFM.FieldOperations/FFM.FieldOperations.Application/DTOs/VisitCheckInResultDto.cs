using System;

namespace FFM.FieldOperations.Application.DTOs
{
    /// <summary>
    /// Data contract returned upon successful on-site check-in verification.
    /// </summary>
    public class VisitCheckInResultDto
    {
        public int VisitId { get; set; }
        public int TaskId { get; set; }
        public string TaskNumber { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public DateTime CheckInTime { get; set; }
        public decimal CheckInLatitude { get; set; }
        public decimal CheckInLongitude { get; set; }
        public decimal DistanceMeters { get; set; }
        public int AllowedRadiusMeters { get; set; }
        public bool IsWithinGeofence { get; set; }
        public string Status { get; set; } = "CHECKED_IN";
        public bool IsIdempotentReplay { get; set; }
    }
}

