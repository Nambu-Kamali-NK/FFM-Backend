namespace FFM.GPS.Tracking.Domain.Entities;
public class LocationBreadcrumb
{
    public long Id {get; set;}
    public int UserId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal AccuracyMeters { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? HeadingDegrees { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public int? BatteryPercentage { get; set; }
    public string ActivityType { get; set; } = "UNKNOWN";
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    //Parameterless constructor for EF Core
    public LocationBreadcrumb()
    {
    }
}