using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Domain.Services;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class CheckInVisitCommandHandler : IRequestHandler<CheckInVisitCommand, VisitCheckInResultDto>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IClientSiteRepository _clientSiteRepository;
        private readonly IVisitRepository _visitRepository;
        private readonly ICurrentUserService _currentUserService;

        public CheckInVisitCommandHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository,
            IVisitRepository visitRepository,
            ICurrentUserService currentUserService)
        {
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
            _visitRepository = visitRepository;
            _currentUserService = currentUserService;
        }

        public async Task<VisitCheckInResultDto> Handle(CheckInVisitCommand request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // 1. Task ID Validation
            if (request.TaskId <= 0)
                throw new ArgumentException("Valid TaskId is required.", nameof(request.TaskId));

            // 2. Coordinate Bounds Validation (BR-CHK-07)
            if (request.Latitude < -90m || request.Latitude > 90m)
                throw new ArgumentException("Latitude must be between -90.0 and +90.0.", nameof(request.Latitude));

            if (request.Longitude < -180m || request.Longitude > 180m)
                throw new ArgumentException("Longitude must be between -180.0 and +180.0.", nameof(request.Longitude));

            // 3. Accuracy Validation
            if (request.AccuracyMeters.HasValue && request.AccuracyMeters.Value < 0)
                throw new ArgumentException("AccuracyMeters cannot be negative.", nameof(request.AccuracyMeters));

            // 4. Anti-Mock Location Guard (BR-CHK-08)
            if (request.IsMockLocation == true)
                throw new InvalidOperationException("Mock GPS or simulated location detected on device.");

            // 5. Retrieve Task
            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                throw new KeyNotFoundException($"Task with ID {request.TaskId} was not found.");
            }

            // 6. Active Site Guard (BR-CHK-02)
            var clientSite = task.ClientSite ?? await _clientSiteRepository.GetByIdAsync(task.ClientSiteId);
            if (clientSite == null || !clientSite.IsActive)
            {
                throw new InvalidOperationException("Task's client site is either missing or inactive.");
            }

            // 7. Task State & Assignment Pre-conditions (BR-CHK-04)
            if (task.Status == "CREATED" || !task.AssignedEmployeeId.HasValue)
            {
                throw new InvalidOperationException("Cannot check in to an unassigned task. Task must be assigned prior to check-in.");
            }

            if (task.Status == "COMPLETED" || task.Status == "CANCELLED")
            {
                throw new InvalidOperationException($"Cannot check in: Task is currently in '{task.Status}' status.");
            }

            if (task.Status != "ASSIGNED" && task.Status != "EN_ROUTE" && task.Status != "CHECKED_IN")
            {
                throw new InvalidOperationException($"Cannot check in to task from status '{task.Status}'. Valid transitions are permitted from ASSIGNED or EN_ROUTE status.");
            }

            // 8. BOLA / Ownership & Role Guard (BR-CHK-03)
            var callerRoles = (request.ActorRoles != null && request.ActorRoles.Any())
                ? request.ActorRoles.ToList()
                : _currentUserService.GetRoles().ToList();

            var callerUserId = request.ActorUserId > 0
                ? request.ActorUserId
                : _currentUserService.GetUserId();

            bool isManagement = callerRoles.Any(r =>
                r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SUPERVISOR", StringComparison.OrdinalIgnoreCase));

            if (!isManagement)
            {
                bool isEmployee = callerRoles.Any(r => r.Equals("EMPLOYEE", StringComparison.OrdinalIgnoreCase));
                if (!isEmployee)
                {
                    throw new UnauthorizedAccessException("Access denied: Caller does not possess an authorized role to execute visit check-in.");
                }

                if (task.AssignedEmployeeId.Value != callerUserId)
                {
                    throw new UnauthorizedAccessException($"Access denied: You are not authorized to check in to this task. Caller employee ID {callerUserId} does not match assigned technician ID {task.AssignedEmployeeId.Value}.");
                }
            }

            // 9. Idempotency Guard (Network Retry / Double-tap)
            if (task.Status == "CHECKED_IN")
            {
                var existingVisit = await _visitRepository.GetByTaskIdAsync(task.Id, cancellationToken);
                if (existingVisit != null && existingVisit.Status == "CHECKED_IN")
                {
                    return new VisitCheckInResultDto
                    {
                        VisitId = existingVisit.Id,
                        TaskId = task.Id,
                        TaskNumber = task.TaskNumber,
                        EmployeeId = existingVisit.EmployeeId,
                        CheckInTime = existingVisit.CheckInTime,
                        CheckInLatitude = existingVisit.CheckInLatitude,
                        CheckInLongitude = existingVisit.CheckInLongitude,
                        DistanceMeters = existingVisit.CheckInDistanceMeters,
                        AllowedRadiusMeters = clientSite.RadiusMeters,
                        IsWithinGeofence = existingVisit.IsWithinGeofence,
                        Status = existingVisit.Status,
                        IsIdempotentReplay = true
                    };
                }
            }

            // 10. Single Active Visit Guard (BR-CHK-06)
            var activeVisit = await _visitRepository.GetActiveVisitByEmployeeIdAsync(task.AssignedEmployeeId.Value, cancellationToken);
            if (activeVisit != null && activeVisit.TaskId != task.Id)
            {
                throw new InvalidOperationException($"Technician already has an active checked-in visit (Visit ID: {activeVisit.Id}, Task ID: {activeVisit.TaskId}). Please complete or checkout the current visit before starting a new one.");
            }

            // 11. Geofence Boundary Check via Haversine Formula (BR-CHK-05)
            var distance = HaversineDistanceCalculator.CalculateDistanceMeters(
                request.Latitude, request.Longitude,
                clientSite.Latitude, clientSite.Longitude);

            var distanceMeters = Math.Round((decimal)distance, 2);
            var isWithinGeofence = distanceMeters <= clientSite.RadiusMeters;

            if (!isWithinGeofence)
            {
                throw new InvalidOperationException(
                    $"Check-in rejected: You are {distanceMeters:N2} meters away from the client site. Maximum allowable radius is {clientSite.RadiusMeters} meters.");
            }

            // 12. Domain State Mutation & Persistence
            task.CheckIn();

            var visit = new Visit(
                taskId: task.Id,
                employeeId: task.AssignedEmployeeId.Value,
                checkInTime: DateTime.UtcNow,
                checkInLatitude: request.Latitude,
                checkInLongitude: request.Longitude,
                checkInDistanceMeters: distanceMeters,
                isWithinGeofence: true);

            await _visitRepository.AddVisitAsync(visit, cancellationToken);
            await _taskRepository.SaveChangesAsync(cancellationToken);

            return new VisitCheckInResultDto
            {
                VisitId = visit.Id,
                TaskId = task.Id,
                TaskNumber = task.TaskNumber,
                EmployeeId = visit.EmployeeId,
                CheckInTime = visit.CheckInTime,
                CheckInLatitude = visit.CheckInLatitude,
                CheckInLongitude = visit.CheckInLongitude,
                DistanceMeters = visit.CheckInDistanceMeters,
                AllowedRadiusMeters = clientSite.RadiusMeters,
                IsWithinGeofence = visit.IsWithinGeofence,
                Status = visit.Status,
                IsIdempotentReplay = false
            };
        }
    }
}
