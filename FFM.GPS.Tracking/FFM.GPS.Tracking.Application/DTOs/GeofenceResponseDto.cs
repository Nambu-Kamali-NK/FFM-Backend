using System;
using System.Collections.Generic;

namespace FFM.GPS.Tracking.Application.DTOs
{
    public class GeofenceResponseDto
    {
        public int Id { get; set; }
        public string LocationRef { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double? RadiusMeters { get; set; }
        public List<CoordinatePoint> Coordinates { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AssignGeofenceRequest
    {
        public int EmployeeId { get; set; }
        public int GeofenceId { get; set; }
    }
}
