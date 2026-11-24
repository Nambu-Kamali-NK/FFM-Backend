using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, TaskDto>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IClientSiteRepository _clientSiteRepository;
        private readonly IVisitRepository? _visitRepository;
        private readonly ICurrentUserService _currentUserService;

        public UpdateTaskStatusCommandHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository,
            ICurrentUserService currentUserService)
            : this(taskRepository, clientSiteRepository, null, currentUserService)
        {
        }

        public UpdateTaskStatusCommandHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository,
            IVisitRepository? visitRepository,
            ICurrentUserService currentUserService)
        {
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
            _visitRepository = visitRepository;
            _currentUserService = currentUserService;
        }

        public async Task<TaskDto> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
        {
            // 1. Task Existence & Parameter Validation (BR-STS-01)
            if (request.TaskId <= 0)
            {
                throw new ArgumentException("Valid TaskId is required.", nameof(request.TaskId));
            }

            // 2. Status Target Validation (BR-STS-02)
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                throw new ArgumentException("Status is required.", nameof(request.Status));
            }

            var normalizedStatus = request.Status.Trim().ToUpperInvariant();
            if (normalizedStatus != "EN_ROUTE" && normalizedStatus != "COMPLETED")
            {
                throw new ArgumentException($"Status '{request.Status}' is invalid. Permitted status for this endpoint is 'EN_ROUTE' or 'COMPLETED'.", nameof(request.Status));
            }

            // 3. Notes Length Validation (BR-STS-07)
            if (!string.IsNullOrEmpty(request.Notes) && request.Notes.Trim().Length > 500)
            {
                throw new ArgumentException("Notes cannot exceed 500 characters.", nameof(request.Notes));
            }

            // 4. Retrieve Task Entity
            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                throw new KeyNotFoundException($"Task with ID {request.TaskId} was not found.");
            }

            // 5. BOLA / Ownership & Role Guard (BR-STS-03)
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
                    throw new UnauthorizedAccessException("Access denied: Caller does not possess an authorized role to update task status.");
                }

                if (!task.AssignedEmployeeId.HasValue || task.AssignedEmployeeId.Value != callerUserId)
                {
                    throw new UnauthorizedAccessException($"Access denied: You are not authorized to update tasks assigned to another employee. Caller employee ID {callerUserId} does not match assigned technician ID {task.AssignedEmployeeId}.");
                }
            }

            // 6. State Machine Progression (BR-STS-04)
            if (normalizedStatus == "EN_ROUTE")
            {
                task.StartTravel();
            }
            else if (normalizedStatus == "COMPLETED")
            {
                task.Complete();

                if (_visitRepository != null)
                {
                    var activeVisit = await _visitRepository.GetByTaskIdAsync(task.Id, cancellationToken);
                    if (activeVisit != null && activeVisit.Status == "CHECKED_IN")
                    {
                        activeVisit.Complete(
                            checkOutTime: DateTime.UtcNow,
                            checkOutLatitude: null,
                            checkOutLongitude: null,
                            completionNotes: !string.IsNullOrWhiteSpace(request.Notes) ? request.Notes : "Task completed successfully.",
                            customerName: null,
                            customerSignatureUrl: null);
                        await _visitRepository.UpdateVisitAsync(activeVisit, cancellationToken);
                    }
                }
            }

            await _taskRepository.SaveChangesAsync(cancellationToken);

            var site = task.ClientSite ?? await _clientSiteRepository.GetByIdAsync(task.ClientSiteId);

            return new TaskDto
            {
                Id = task.Id,
                TaskNumber = task.TaskNumber,
                Title = task.Title,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status,
                ClientSiteId = task.ClientSiteId,
                ClientSiteName = site?.Name ?? string.Empty,
                ClientSiteCode = site?.SiteCode ?? string.Empty,
                Address = site?.Address ?? string.Empty,
                Latitude = site?.Latitude ?? 0m,
                Longitude = site?.Longitude ?? 0m,
                RadiusMeters = site?.RadiusMeters ?? 0,
                AssignedEmployeeId = task.AssignedEmployeeId,
                ScheduledStart = task.ScheduledStart,
                ScheduledEnd = task.ScheduledEnd,
                CreatedByUserId = task.CreatedByUserId,
                CancellationReason = task.CancellationReason,
                CancelledAt = task.CancelledAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
        }
    }
}
