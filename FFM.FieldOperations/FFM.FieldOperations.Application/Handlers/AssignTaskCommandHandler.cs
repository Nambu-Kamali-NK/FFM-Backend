using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class AssignTaskCommandHandler : IRequestHandler<AssignTaskCommand, TaskDto>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IClientSiteRepository _clientSiteRepository;
        private readonly ITaskNotificationDispatcher? _notificationDispatcher;

        public AssignTaskCommandHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository,
            ITaskNotificationDispatcher? notificationDispatcher = null)
        {
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
            _notificationDispatcher = notificationDispatcher;
        }

        public async Task<TaskDto> Handle(AssignTaskCommand request, CancellationToken cancellationToken)
        {
            // 1. Validate Input IDs
            if (request.TaskId <= 0)
            {
                throw new ArgumentException("Valid TaskId is required.", nameof(request.TaskId));
            }

            if (request.AssignedEmployeeId <= 0)
            {
                throw new ArgumentException("AssignedEmployeeId must be a positive integer greater than zero.", nameof(request.AssignedEmployeeId));
            }

            // 2. Retrieve Task
            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                throw new KeyNotFoundException($"Task with ID {request.TaskId} was not found.");
            }

            // 3. Domain Logic: Assign or Reassign
            task.Assign(request.AssignedEmployeeId);

            // 4. Persist Changes
            await _taskRepository.SaveChangesAsync(cancellationToken);

            // 5. Ensure ClientSite navigation is resolved
            var site = task.ClientSite ?? await _clientSiteRepository.GetByIdAsync(task.ClientSiteId);

            // 6. Trigger Notification Dispatch (Non-blocking resilience)
            if (_notificationDispatcher != null)
            {
                try
                {
                    await _notificationDispatcher.DispatchTaskAssignmentNotificationAsync(
                        task.Id,
                        task.TaskNumber,
                        task.Title,
                        task.AssignedEmployeeId ?? request.AssignedEmployeeId,
                        site?.Name ?? "Municipal Ward",
                        task.Priority,
                        cancellationToken);
                }
                catch
                {
                    // Resilient: Core task assignment transaction succeeds even if notification endpoint is warming up
                }
            }

            // 7. Map and Return DTO
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
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            };
        }
    }
}
