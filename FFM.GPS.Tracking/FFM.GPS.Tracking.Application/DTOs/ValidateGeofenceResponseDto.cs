namespace FFM.GPS.Tracking.Application.DTOs
{
    public class ValidateGeofenceResponseDto
    {
        public bool IsValidGeofence { get; set; }
        public string? LocationRef { get; set; }
        public string? SiteName { get; set; }
        public double? DistanceFromCenterMeters { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
