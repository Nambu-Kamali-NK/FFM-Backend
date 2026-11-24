using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetVisitEvidencesQueryHandler : IRequestHandler<GetVisitEvidencesQuery, IReadOnlyList<VisitEvidenceDto>>
    {
        private readonly IVisitRepository _visitRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly ICurrentUserService _currentUserService;

        public GetVisitEvidencesQueryHandler(
            IVisitRepository visitRepository,
            ITaskRepository taskRepository,
            ICurrentUserService currentUserService)
        {
            _visitRepository = visitRepository;
            _taskRepository = taskRepository;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<VisitEvidenceDto>> Handle(GetVisitEvidencesQuery request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!request.VisitId.HasValue && !request.TaskId.HasValue)
            {
                throw new ArgumentException("Either VisitId or TaskId must be specified to retrieve visit evidences.");
            }

            var callerRoles = (request.CallerRoles != null && request.CallerRoles.Any())
                ? request.CallerRoles.ToList()
                : _currentUserService.GetRoles().ToList();

            var callerUserId = request.CallerUserId > 0
                ? request.CallerUserId
                : _currentUserService.GetUserId();

            bool isManagement = callerRoles.Any(r =>
                r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SUPERVISOR", StringComparison.OrdinalIgnoreCase));

            IReadOnlyList<VisitEvidence> evidences;

            if (request.VisitId.HasValue)
            {
                var visit = await _visitRepository.GetByIdAsync(request.VisitId.Value, cancellationToken);
                if (visit == null)
                {
                    throw new KeyNotFoundException($"Visit with ID {request.VisitId.Value} was not found.");
                }

                if (!isManagement)
                {
                    if (visit.EmployeeId != callerUserId)
                    {
                        throw new UnauthorizedAccessException($"Access denied: You are not authorized to view evidences for visit {request.VisitId.Value}.");
                    }
                }

                evidences = await _visitRepository.GetEvidencesByVisitIdAsync(request.VisitId.Value, request.EvidenceType, cancellationToken);
            }
            else
            {
                var task = await _taskRepository.GetByIdAsync(request.TaskId!.Value, cancellationToken);
                if (task == null)
                {
                    throw new KeyNotFoundException($"Task with ID {request.TaskId.Value} was not found.");
                }

                if (!isManagement)
                {
                    if (task.AssignedEmployeeId != callerUserId)
                    {
                        throw new UnauthorizedAccessException($"Access denied: You are not authorized to view evidences for task {request.TaskId.Value}.");
                    }
                }

                evidences = await _visitRepository.GetEvidencesByTaskIdAsync(request.TaskId.Value, request.EvidenceType, cancellationToken);
            }

            return evidences.Select(e => new VisitEvidenceDto
            {
                Id = e.Id,
                VisitId = e.VisitId,
                EvidenceTypeId = e.EvidenceTypeId,
                EvidenceType = e.EvidenceType,
                FileUrl = e.FileUrl,
                ThumbnailUrl = e.ThumbnailUrl,
                Remarks = e.Remarks,
                CapturedLatitude = e.CapturedLatitude,
                CapturedLongitude = e.CapturedLongitude,
                CapturedAt = e.CapturedAt,
                Readings = !string.IsNullOrWhiteSpace(e.ReadingsJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(e.ReadingsJson)
                    : null
            }).ToList();
        }
    }
}

