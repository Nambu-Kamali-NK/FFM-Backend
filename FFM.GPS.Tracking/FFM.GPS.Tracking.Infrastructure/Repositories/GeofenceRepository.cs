using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FFM.GPS.Tracking.Infrastructure.Repositories
{
    public class GeofenceRepository : IGeofenceRepository
    {
        private readonly GpsDbContext _dbContext;

        public GeofenceRepository(GpsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Geofence?> GetByIdAsync(int id)
        {
            return await _dbContext.Geofences.FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<Geofence?> GetByLocationRefAsync(string locationRef)
        {
            return await _dbContext.Geofences.FirstOrDefaultAsync(g => g.LocationRef == locationRef);
        }

        public async Task<IEnumerable<Geofence>> GetAllActiveAsync()
        {
            return await _dbContext.Geofences.Where(g => g.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<Geofence>> GetAssignedGeofencesByEmployeeIdAsync(int employeeId)
        {
            return await _dbContext.EmployeeGeofences
                .Where(eg => eg.EmployeeId == employeeId)
                .Include(eg => eg.Geofence)
                .Where(eg => eg.Geofence.IsActive)
                .Select(eg => eg.Geofence)
                .ToListAsync();
        }

        public async Task AddGeofenceAsync(Geofence geofence)
        {
            await _dbContext.Geofences.AddAsync(geofence);
        }

        public async Task AssignGeofenceToEmployeeAsync(EmployeeGeofence employeeGeofence)
        {
            await _dbContext.EmployeeGeofences.AddAsync(employeeGeofence);
        }

        public async Task<bool> IsEmployeeMappedToGeofenceAsync(int employeeId, int geofenceId)
        {
            return await _dbContext.EmployeeGeofences
                .AnyAsync(eg => eg.EmployeeId == employeeId && eg.GeofenceId == geofenceId);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
