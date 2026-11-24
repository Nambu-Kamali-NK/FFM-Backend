using System;
using System.Linq;
using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Domain.Services
{
    public class GeofenceValidationService : IGeofenceValidationService
    {
        private const double EarthRadiusMeters = 6371000.0;

        public double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return EarthRadiusMeters * c;
        }

        public bool IsPointInCircularGeofence(
            double pointLat,
            double pointLon,
            double centerLat,
            double centerLon,
            double radiusMeters,
            double bufferMeters)
        {
            var distance = CalculateHaversineDistanceMeters(pointLat, pointLon, centerLat, centerLon);
            return distance <= (radiusMeters + bufferMeters);
        }

        public bool IsPointInPolygonGeofence(double pointLat, double pointLon, CoordinateVertex[] polygonVertices)
        {
            if (polygonVertices == null || polygonVertices.Length < 3)
                return false;

            var tuples = polygonVertices.Select(v => (v.Lat, v.Lon)).ToArray();
            return IsPointInPolygonGeofence(pointLat, pointLon, tuples);
        }

        public bool IsPointInPolygonGeofence(double pointLat, double pointLon, (double Lat, double Lon)[] polygonVertices)
        {
            if (polygonVertices == null || polygonVertices.Length < 3)
                return false;

            bool isInside = false;
            int n = polygonVertices.Length;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var xi = polygonVertices[i].Lon;
                var yi = polygonVertices[i].Lat;
                var xj = polygonVertices[j].Lon;
                var yj = polygonVertices[j].Lat;

                bool intersect = ((yi > pointLat) != (yj > pointLat)) &&
                                 (pointLon < (xj - xi) * (pointLat - yi) / (yj - yi) + xi);

                if (intersect)
                    isInside = !isInside;
            }

            return isInside;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
