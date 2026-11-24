using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class CancelTaskCommandHandler : IRequestHandler<CancelTaskCommand, TaskDto>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IClientSiteRepository _clientSiteRepository;

        public CancelTaskCommandHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository)
        {
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
        }

        public async Task<TaskDto> Handle(CancelTaskCommand request, CancellationToken cancellationToken)
        {
            if (request.TaskId <= 0)
            {
                throw new ArgumentException("Valid TaskId is required.", nameof(request.TaskId));
            }

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
            {
                throw new ArgumentException("Cancellation reason must be provided (at least 5 characters).", nameof(request.Reason));
            }

            if (request.Reason.Trim().Length > 500)
            {
                throw new ArgumentException("Cancellation reason cannot exceed 500 characters.", nameof(request.Reason));
            }

            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
            {
                throw new KeyNotFoundException($"Task with ID {request.TaskId} was not found.");
            }

            // Domain state machine guard and cancellation
            task.Cancel(request.Reason);

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
