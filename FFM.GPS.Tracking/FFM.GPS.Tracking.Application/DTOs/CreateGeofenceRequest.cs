using System.Collections.Generic;

namespace FFM.GPS.Tracking.Application.DTOs
{
    public class CreateGeofenceRequest
    {
        public string LocationRef { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "CIRCULAR"; // CIRCULAR or POLYGON
        public double? RadiusMeters { get; set; }
        public List<CoordinatePoint> Coordinates { get; set; } = new();
    }
}
