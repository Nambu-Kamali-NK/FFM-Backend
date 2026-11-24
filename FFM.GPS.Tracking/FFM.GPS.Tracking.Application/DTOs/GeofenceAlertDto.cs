using System;

namespace FFM.GPS.Tracking.Application.DTOs
{
    public class GeofenceAlertDto
    {
        public long BreachId { get; set; }
        public int UserId { get; set; }
        public int GeofenceId { get; set; }
        public string GeofenceName { get; set; } = string.Empty;
        public string LocationRef { get; set; } = string.Empty;
        public string BreachType { get; set; } = "EXIT"; // EXIT or ENTRY
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? AccuracyMeters { get; set; }
        public double? DistanceFromBoundaryMeters { get; set; }
        public decimal? SpeedKmh { get; set; }
        public string Severity { get; set; } = "HIGH"; // CRITICAL, HIGH, INFO
        public string Message { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; }
    }
}
