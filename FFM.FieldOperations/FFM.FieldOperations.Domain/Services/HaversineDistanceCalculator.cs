using System;

namespace FFM.FieldOperations.Domain.Services
{
    /// <summary>
    /// Computes great-circle distance between two GPS coordinates on the WGS-84 reference sphere.
    /// Formula adheres to the ISO/IEC/IEEE geofencing requirements.
    /// </summary>
    public static class HaversineDistanceCalculator
    {
        public const double EarthRadiusMeters = 6371000.0;

        public static double CalculateDistanceMeters(
            double lat1, double lon1,
            double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return EarthRadiusMeters * c;
        }

        public static double CalculateDistanceMeters(
            decimal lat1, decimal lon1,
            decimal lat2, decimal lon2)
        {
            return CalculateDistanceMeters((double)lat1, (double)lon1, (double)lat2, (double)lon2);
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}

