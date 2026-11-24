using FFM.GPS.Tracking.Domain.Services;
using Xunit;

namespace FFM.GPS.Tracking.Tests
{
    public class GeofenceValidationServiceTests
    {
        private readonly GeofenceValidationService _service = new();

        [Fact]
        public void CalculateHaversineDistanceMeters_SameCoordinates_ReturnsZero()
        {
            double lat = 13.0827;
            double lon = 80.2707;

            double distance = _service.CalculateHaversineDistanceMeters(lat, lon, lat, lon);

            Assert.Equal(0.0, distance, precision: 2);
        }

        [Fact]
        public void IsPointInCircularGeofence_InsideRadius_ReturnsTrue()
        {
            // Center: Chennai (13.0827, 80.2707)
            double centerLat = 13.0827;
            double centerLon = 80.2707;
            double radiusMeters = 100.0;
            double bufferMeters = 15.0;

            // Point ~20 meters away
            double pointLat = 13.0828;
            double pointLon = 80.2708;

            bool isInside = _service.IsPointInCircularGeofence(pointLat, pointLon, centerLat, centerLon, radiusMeters, bufferMeters);

            Assert.True(isInside);
        }

        [Fact]
        public void IsPointInCircularGeofence_OutsideRadiusAndBuffer_ReturnsFalse()
        {
            double centerLat = 13.0827;
            double centerLon = 80.2707;
            double radiusMeters = 100.0;
            double bufferMeters = 15.0;

            // Point ~2.5 km away
            double pointLat = 13.1000;
            double pointLon = 80.2900;

            bool isInside = _service.IsPointInCircularGeofence(pointLat, pointLon, centerLat, centerLon, radiusMeters, bufferMeters);

            Assert.False(isInside);
        }

        [Fact]
        public void IsPointInPolygonGeofence_InsideSquarePolygon_ReturnsTrue()
        {
            // Square polygon around (13.0, 80.0)
            var vertices = new (double Lat, double Lon)[]
            {
                (13.00, 80.00),
                (13.10, 80.00),
                (13.10, 80.10),
                (13.00, 80.10)
            };

            double pointLat = 13.05;
            double pointLon = 80.05;

            bool isInside = _service.IsPointInPolygonGeofence(pointLat, pointLon, vertices);

            Assert.True(isInside);
        }

        [Fact]
        public void IsPointInPolygonGeofence_OutsideSquarePolygon_ReturnsFalse()
        {
            var vertices = new (double Lat, double Lon)[]
            {
                (13.00, 80.00),
                (13.10, 80.00),
                (13.10, 80.10),
                (13.00, 80.10)
            };

            double pointLat = 13.20;
            double pointLon = 80.20;

            bool isInside = _service.IsPointInPolygonGeofence(pointLat, pointLon, vertices);

            Assert.False(isInside);
        }
    }
}
