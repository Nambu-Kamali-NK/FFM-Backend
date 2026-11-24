using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.GPS.Tracking.Application.DTOs;
using FFM.GPS.Tracking.Domain.Entities;
using FFM.GPS.Tracking.Domain.Interfaces;
using MediatR;

namespace FFM.GPS.Tracking.Application.Commands
{
    public class AssignGeofenceCommand : IRequest<bool>
    {
        public int EmployeeId { get; set; }
        public int GeofenceId { get; set; }
    }

    public class AssignGeofenceCommandHandler : IRequestHandler<AssignGeofenceCommand, bool>
    {
        private readonly IGeofenceRepository _geofenceRepository;

        public AssignGeofenceCommandHandler(IGeofenceRepository geofenceRepository)
        {
            _geofenceRepository = geofenceRepository;
        }

        public async Task<bool> Handle(AssignGeofenceCommand request, CancellationToken cancellationToken)
        {
            var geofence = await _geofenceRepository.GetByIdAsync(request.GeofenceId);
            if (geofence == null)
            {
                throw new KeyNotFoundException($"Geofence ID {request.GeofenceId} not found.");
            }

            var isAlreadyMapped = await _geofenceRepository.IsEmployeeMappedToGeofenceAsync(request.EmployeeId, request.GeofenceId);
            if (isAlreadyMapped)
            {
                throw new InvalidOperationException($"Employee ID {request.EmployeeId} is already mapped to Geofence ID {request.GeofenceId}.");
            }

            var mapping = new EmployeeGeofence(request.EmployeeId, request.GeofenceId);
            await _geofenceRepository.AssignGeofenceToEmployeeAsync(mapping);
            await _geofenceRepository.SaveChangesAsync();

            return true;
        }
    }
}
