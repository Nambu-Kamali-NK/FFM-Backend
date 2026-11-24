using System;

namespace FFM.GPS.Tracking.Domain.Entities
{
    public class EmployeeGeofence
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public int GeofenceId { get; private set; }
        public DateTime AssignedAt { get; private set; }

        public Geofence Geofence { get; private set; } = null!;

        private EmployeeGeofence() { }

        public EmployeeGeofence(int employeeId, int geofenceId)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Invalid Employee ID.", nameof(employeeId));

            if (geofenceId <= 0)
                throw new ArgumentException("Invalid Geofence ID.", nameof(geofenceId));

            EmployeeId = employeeId;
            GeofenceId = geofenceId;
            AssignedAt = DateTime.UtcNow;
        }
    }
}
