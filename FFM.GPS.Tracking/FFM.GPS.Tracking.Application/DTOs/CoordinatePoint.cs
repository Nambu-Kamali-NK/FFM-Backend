namespace FFM.GPS.Tracking.Application.DTOs
{
    public class CoordinatePoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public CoordinatePoint() { }

        public CoordinatePoint(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
