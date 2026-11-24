namespace FFM.GPS.Tracking.Domain.Entities
{
    public class CoordinateVertex
    {
        private double _lat;
        private double _lon;

        public double Lat
        {
            get => _lat;
            set => _lat = value;
        }

        public double Lon
        {
            get => _lon;
            set => _lon = value;
        }

        public double Latitude
        {
            get => _lat;
            set => _lat = value;
        }

        public double Longitude
        {
            get => _lon;
            set => _lon = value;
        }

        public CoordinateVertex() { }

        public CoordinateVertex(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }
    }
}
