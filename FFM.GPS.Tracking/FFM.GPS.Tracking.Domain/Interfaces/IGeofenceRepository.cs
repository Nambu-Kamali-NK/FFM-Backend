using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Domain.Entities;

namespace FFM.GPS.Tracking.Domain.Interfaces
{
    public interface IGeofenceRepository
    {
        Task<Geofence?> GetByIdAsync(int id);
        Task<Geofence?> GetByLocationRefAsync(string locationRef);
        Task<IEnumerable<Geofence>> GetAllActiveAsync();
        Task<IEnumerable<Geofence>> GetAssignedGeofencesByEmployeeIdAsync(int employeeId);
        Task AddGeofenceAsync(Geofence geofence);
        Task AssignGeofenceToEmployeeAsync(EmployeeGeofence employeeGeofence);
        Task<bool> IsEmployeeMappedToGeofenceAsync(int employeeId, int geofenceId);
        Task SaveChangesAsync();
    }
}
