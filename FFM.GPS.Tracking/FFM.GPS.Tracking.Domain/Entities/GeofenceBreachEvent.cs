using System;

namespace FFM.GPS.Tracking.Domain.Entities
{
    public class GeofenceBreachEvent
    {
        public long Id { get; private set; }
        public int UserId { get; private set; }
        public int GeofenceId { get; private set; }
        public string GeofenceName { get; private set; } = string.Empty;
        public string LocationRef { get; private set; } = string.Empty;
        public string BreachType { get; private set; } = "EXIT"; // "EXIT" or "ENTRY"
        public decimal Latitude { get; private set; }
        public decimal Longitude { get; private set; }
        public decimal? AccuracyMeters { get; private set; }
        public double? DistanceFromBoundaryMeters { get; private set; }
        public decimal? SpeedKmh { get; private set; }
        public DateTime TriggeredAt { get; private set; }
        public bool IsAcknowledged { get; private set; } = false;
        public string? AcknowledgedBy { get; private set; }
        public DateTime? AcknowledgedAt { get; private set; }
        public string? Notes { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Geofence? Geofence { get; private set; }

        // Parameterless constructor for EF Core
        private GeofenceBreachEvent() { }

        public GeofenceBreachEvent(
            int userId,
            int geofenceId,
            string geofenceName,
            string locationRef,
            string breachType,
            decimal latitude,
            decimal longitude,
            decimal? accuracyMeters,
            double? distanceFromBoundaryMeters,
            decimal? speedKmh,
            DateTime triggeredAt)
        {
            if (userId <= 0)
                throw new ArgumentException("Invalid User ID.", nameof(userId));

            if (geofenceId <= 0)
                throw new ArgumentException("Invalid Geofence ID.", nameof(geofenceId));

            UserId = userId;
            GeofenceId = geofenceId;
            GeofenceName = geofenceName ?? string.Empty;
            LocationRef = locationRef ?? string.Empty;
            BreachType = string.IsNullOrWhiteSpace(breachType) ? "EXIT" : breachType.ToUpperInvariant();
            Latitude = latitude;
            Longitude = longitude;
            AccuracyMeters = accuracyMeters;
            DistanceFromBoundaryMeters = distanceFromBoundaryMeters;
            SpeedKmh = speedKmh;
            TriggeredAt = triggeredAt == default ? DateTime.UtcNow : triggeredAt;
            IsAcknowledged = false;
            CreatedAt = DateTime.UtcNow;
        }

        public void Acknowledge(string acknowledgedBy, string? notes)
        {
            if (string.IsNullOrWhiteSpace(acknowledgedBy))
                throw new ArgumentException("AcknowledgedBy must be provided.", nameof(acknowledgedBy));

            IsAcknowledged = true;
            AcknowledgedBy = acknowledgedBy.Trim();
            Notes = notes?.Trim();
            AcknowledgedAt = DateTime.UtcNow;
        }
    }
}
