using System.Threading.Tasks;

namespace FFM.Workforce.Application.Interfaces
{
    public class GeofenceValidationResult
    {
        public bool IsValidGeofence { get; set; }
        public string? LocationRef { get; set; }
        public string? SiteName { get; set; }
        public double? DistanceFromCenterMeters { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public interface IGeofenceValidationClient
    {
        Task<GeofenceValidationResult> ValidateLocationAsync(int employeeId, double latitude, double longitude, double gpsAccuracyMeters = 10.0);
    }
}
