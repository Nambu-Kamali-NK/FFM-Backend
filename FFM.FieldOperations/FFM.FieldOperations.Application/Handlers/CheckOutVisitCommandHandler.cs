using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class CheckOutVisitCommandHandler : IRequestHandler<CheckOutVisitCommand, VisitCheckOutResultDto>
    {
        private readonly IVisitRepository _visitRepository;
        private readonly ITaskRepository _taskRepository;

        public CheckOutVisitCommandHandler(
            IVisitRepository visitRepository,
            ITaskRepository taskRepository)
        {
            _visitRepository = visitRepository;
            _taskRepository = taskRepository;
        }

        public async Task<VisitCheckOutResultDto> Handle(CheckOutVisitCommand request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // BR-CO-09: Coordinate bounds check
            if (request.Latitude < -90m || request.Latitude > 90m)
                throw new ArgumentException("Latitude must be between -90.0 and 90.0.", nameof(request.Latitude));

            if (request.Longitude < -180m || request.Longitude > 180m)
                throw new ArgumentException("Longitude must be between -180.0 and 180.0.", nameof(request.Longitude));

            // BR-CO-05: Mandatory completion notes check
            if (string.IsNullOrWhiteSpace(request.CompletionNotes) || request.CompletionNotes.Trim().Length < 5)
                throw new ArgumentException("Completion notes are mandatory and must be at least 5 characters.", nameof(request.CompletionNotes));

            if (request.CompletionNotes.Length > 1000)
                throw new ArgumentException("Completion notes cannot exceed 1000 characters.", nameof(request.CompletionNotes));

            // BR-CO-06: Customer name length check
            if (!string.IsNullOrEmpty(request.CustomerName) && request.CustomerName.Trim().Length > 100)
                throw new ArgumentException("Customer name cannot exceed 100 characters.", nameof(request.CustomerName));

            // BR-CO-07: Customer signature URL bounds
            if (!string.IsNullOrEmpty(request.CustomerSignatureUrl))
            {
                if (request.CustomerSignatureUrl.Length > 500)
                    throw new ArgumentException("Customer signature URL cannot exceed 500 characters.", nameof(request.CustomerSignatureUrl));

                if (!Uri.TryCreate(request.CustomerSignatureUrl, UriKind.Absolute, out var uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException("Customer signature URL must be a valid HTTP or HTTPS URL.", nameof(request.CustomerSignatureUrl));
                }
            }

            // BR-CO-08: Customer rating bounds
            if (request.CustomerRating.HasValue && (request.CustomerRating.Value < 1 || request.CustomerRating.Value > 5))
                throw new ArgumentException("Customer rating must be an integer between 1 and 5.", nameof(request.CustomerRating));

            // BR-CO-01: Visit existence check
            Visit? visit = null;
            if (request.VisitId.HasValue && request.VisitId.Value > 0)
            {
                visit = await _visitRepository.GetByIdAsync(request.VisitId.Value, cancellationToken);
            }
            else if (request.TaskId.HasValue && request.TaskId.Value > 0)
            {
                visit = await _visitRepository.GetByTaskIdAsync(request.TaskId.Value, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Either VisitId or TaskId must be provided.", nameof(request.VisitId));
            }

            if (visit == null)
            {
                if (request.TaskId.HasValue && request.TaskId.Value > 0)
                {
                    var directTask = await _taskRepository.GetByIdAsync(request.TaskId.Value, cancellationToken);
                    if (directTask == null)
                        throw new KeyNotFoundException($"Task with ID {request.TaskId.Value} was not found.");

                    directTask.Complete();
                    await _taskRepository.SaveChangesAsync(cancellationToken);

                    return new VisitCheckOutResultDto
                    {
                        VisitId = 0,
                        TaskId = directTask.Id,
                        TaskNumber = directTask.TaskNumber,
                        CheckInTime = directTask.CreatedAt,
                        CheckOutTime = DateTime.UtcNow,
                        DurationMinutes = 0,
                        TaskStatus = directTask.Status,
                        CompletionNotes = request.CompletionNotes,
                        CustomerName = request.CustomerName,
                        CustomerSignatureUrl = request.CustomerSignatureUrl,
                        CustomerRating = request.CustomerRating,
                        UpdatedAt = directTask.UpdatedAt
                    };
                }

                throw new KeyNotFoundException($"Visit with ID {request.VisitId} was not found.");
            }

            // BR-CO-04: BOLA ownership guard
            bool isPrivileged = request.ActorRoles.Any(r =>
                r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SUPERVISOR", StringComparison.OrdinalIgnoreCase));

            if (!isPrivileged && visit.EmployeeId != request.ActorUserId)
                throw new UnauthorizedAccessException("You do not have permission to check out this visit. Access denied.");

            // BR-CO-02: Visit state guard (Already closed)
            if (visit.CheckOutTime.HasValue || visit.Status != "CHECKED_IN")
                throw new InvalidOperationException($"This visit has already been checked out at {visit.CheckOutTime:O}. Duplicate checkout is not permitted.");

            // BR-CO-03: Task state guard
            var task = visit.Task;
            if (task == null)
            {
                task = await _taskRepository.GetByIdAsync(visit.TaskId);
            }

            if (task == null || task.Status != "CHECKED_IN")
            {
                throw new InvalidOperationException($"Cannot check out visit. Associated task is in '{task?.Status ?? "UNKNOWN"}' status. Must be in 'CHECKED_IN'.");
            }

            // Execute domain completion (BR-FLD-05 evidence check is enforced inside visit.Complete)
            var checkOutTime = DateTime.UtcNow;
            visit.Complete(
                checkOutTime: checkOutTime,
                checkOutLatitude: request.Latitude,
                checkOutLongitude: request.Longitude,
                completionNotes: request.CompletionNotes,
                customerName: request.CustomerName,
                customerSignatureUrl: request.CustomerSignatureUrl,
                customerRating: request.CustomerRating);

            task.Complete();

            await _visitRepository.UpdateVisitAsync(visit, cancellationToken);
            await _taskRepository.SaveChangesAsync(cancellationToken);

            return new VisitCheckOutResultDto
            {
                VisitId = visit.Id,
                TaskId = task.Id,
                TaskNumber = task.TaskNumber,
                CheckInTime = visit.CheckInTime,
                CheckOutTime = visit.CheckOutTime!.Value,
                DurationMinutes = visit.DurationMinutes ?? 0,
                TaskStatus = task.Status,
                CompletionNotes = visit.CompletionNotes ?? string.Empty,
                CustomerName = visit.CustomerName,
                CustomerSignatureUrl = visit.CustomerSignatureUrl,
                CustomerRating = visit.CustomerRating,
                UpdatedAt = task.UpdatedAt
            };
        }
    }
}
