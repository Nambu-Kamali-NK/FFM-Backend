using System;

namespace FFM.GPS.Tracking.Application.DTOs;

public class LocationBreadcrumbStreamDto
{
    public int UserId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? HeadingDegrees { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public int? BatteryPercentage { get; set; }
    public string ActivityType { get; set; } = "UNKNOWN";
    public DateTime RecordedAt { get; set; }
    public DateTime TransmittedAt { get; set; } = DateTime.UtcNow;
}
