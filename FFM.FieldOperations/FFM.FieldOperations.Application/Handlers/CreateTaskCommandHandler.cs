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
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, TaskDto>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IClientSiteRepository _clientSiteRepository;

        public CreateTaskCommandHandler(
            ITaskRepository taskRepository,
            IClientSiteRepository clientSiteRepository)
        {
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
        }

        public async Task<TaskDto> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
        {
            // 1. Validate Title
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length < 5)
            {
                throw new ArgumentException("Title must be at least 5 characters.", nameof(request.Title));
            }

            if (request.Title.Trim().Length > 200)
            {
                throw new ArgumentException("Title cannot exceed 200 characters.", nameof(request.Title));
            }

            if (request.Description != null && request.Description.Trim().Length > 2000)
            {
                throw new ArgumentException("Description cannot exceed 2000 characters.", nameof(request.Description));
            }

            // 2. Normalize and Validate Schedule Chronology
            var startUtc = request.ScheduledStart.Kind == DateTimeKind.Utc
                ? request.ScheduledStart
                : DateTime.SpecifyKind(request.ScheduledStart, DateTimeKind.Utc);

            var endUtc = request.ScheduledEnd.Kind == DateTimeKind.Utc
                ? request.ScheduledEnd
                : DateTime.SpecifyKind(request.ScheduledEnd, DateTimeKind.Utc);

            if (endUtc <= startUtc)
            {
                throw new ArgumentException("ScheduledEnd must be strictly later than ScheduledStart.", nameof(request.ScheduledEnd));
            }

            // 3. Validate Schedule Non-Retrospective
            if (startUtc < DateTime.UtcNow.AddHours(-24))
            {
                throw new ArgumentException("ScheduledStart cannot be set more than 24 hours in the past.", nameof(request.ScheduledStart));
            }

            // 4. Validate Priority
            var validPriorities = new[] { "LOW", "MEDIUM", "HIGH", "URGENT" };
            var normalizedPriority = string.IsNullOrWhiteSpace(request.Priority) ? "MEDIUM" : request.Priority.Trim().ToUpperInvariant();
            if (!validPriorities.Contains(normalizedPriority))
            {
                throw new ArgumentException($"Priority must be one of: LOW, MEDIUM, HIGH, URGENT. Received: '{request.Priority}'", nameof(request.Priority));
            }

            // 4b. Validate Assigned Employee ID (if provided)
            if (request.AssignedEmployeeId.HasValue && request.AssignedEmployeeId.Value <= 0)
            {
                throw new ArgumentException("AssignedEmployeeId must be a positive integer greater than zero.", nameof(request.AssignedEmployeeId));
            }

            // 5. Validate Client Site Existence & Active Status
            if (request.ClientSiteId <= 0)
            {
                throw new ArgumentException("Valid ClientSiteId is required.", nameof(request.ClientSiteId));
            }

            var site = await _clientSiteRepository.GetByIdAsync(request.ClientSiteId);
            if (site == null)
            {
                throw new KeyNotFoundException($"Client site with ID {request.ClientSiteId} was not found.");
            }

            if (!site.IsActive)
            {
                throw new InvalidOperationException($"Client site '{site.Name}' ({site.SiteCode}) is inactive and cannot accept new tasks.");
            }

            // 6. Generate Next Sequential Task Number
            var taskNumber = await _taskRepository.GenerateNextTaskNumberAsync(cancellationToken);

            string? customDataJson = null;
            if (request.CustomData != null && request.CustomData.Count > 0)
            {
                customDataJson = System.Text.Json.JsonSerializer.Serialize(request.CustomData);
            }

            // 7. Instantiate Domain Entity
            var task = new TaskItem(
                taskNumber,
                request.Title,
                request.Description,
                normalizedPriority,
                request.ClientSiteId,
                request.AssignedEmployeeId,
                startUtc,
                endUtc,
                request.ActorUserId,
                request.TaskType ?? "GENERAL",
                customDataJson);

            // 8. Persist to Database
            await _taskRepository.AddAsync(task, cancellationToken);
            await _taskRepository.SaveChangesAsync(cancellationToken);

            // 9. Map and Return Response DTO
            return new TaskDto
            {
                Id = task.Id,
                TaskNumber = task.TaskNumber,
                Title = task.Title,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status,
                TaskType = task.TaskType,
                CustomData = request.CustomData,
                ClientSiteId = site.Id,
                ClientSiteName = site.Name,
                ClientSiteCode = site.SiteCode,
                Address = site.Address,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                RadiusMeters = site.RadiusMeters,
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
