namespace FFM.GPS.Tracking.Application.DTOs
{
    public class ValidateGeofenceRequest
    {
        public int EmployeeId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double GpsAccuracyMeters { get; set; } = 5.0;
    }
}
