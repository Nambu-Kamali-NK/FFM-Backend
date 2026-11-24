using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Domain.Services
{
    public interface IGeofenceValidationService
    {
        double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2);
        bool IsPointInCircularGeofence(double pointLat, double pointLon, double centerLat, double centerLon, double radiusMeters, double bufferMeters);
        bool IsPointInPolygonGeofence(double pointLat, double pointLon, CoordinateVertex[] polygonVertices);
        bool IsPointInPolygonGeofence(double pointLat, double pointLon, (double Lat, double Lon)[] polygonVertices);
    }
}
