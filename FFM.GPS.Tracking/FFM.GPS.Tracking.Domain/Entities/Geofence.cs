using System;

namespace FFM.GPS.Tracking.Domain.Entities
{
    public class Geofence
    {
        public int Id { get; private set; }
        public string LocationRef { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string Type { get; private set; } = "CIRCULAR"; // CIRCULAR or POLYGON
        public double? CenterLatitude { get; private set; }
        public double? CenterLongitude { get; private set; }
        public double? RadiusMeters { get; private set; }
        public double BufferMeters { get; private set; } = 15.0;
        public string? PolygonCoordinatesJson { get; private set; }
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private Geofence() { }

        public Geofence(
            string locationRef,
            string name,
            string type,
            double? centerLatitude,
            double? centerLongitude,
            double? radiusMeters,
            double bufferMeters = 15.0,
            string? polygonCoordinatesJson = null)
        {
            if (string.IsNullOrWhiteSpace(locationRef))
                throw new ArgumentException("LocationRef cannot be empty.", nameof(locationRef));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Geofence Name cannot be empty.", nameof(name));

            LocationRef = locationRef;
            Name = name;
            Type = type.ToUpperInvariant();
            CenterLatitude = centerLatitude;
            CenterLongitude = centerLongitude;
            RadiusMeters = radiusMeters;
            BufferMeters = Math.Max(0, bufferMeters);
            PolygonCoordinatesJson = polygonCoordinatesJson;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
