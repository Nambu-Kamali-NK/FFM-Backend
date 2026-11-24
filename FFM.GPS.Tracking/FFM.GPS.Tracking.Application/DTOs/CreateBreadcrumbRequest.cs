using System;

namespace FFM.GPS.Tracking.Application.DTOs;

public class CreateBreadcrumbRequest
{
    public int UserId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal AccuracyMeters { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? HeadingDegrees { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public int? BatteryPercentage { get; set; }
    public string? ActivityType { get; set; }
    public DateTime RecordedAt { get; set; }
}