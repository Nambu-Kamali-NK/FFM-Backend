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
    public class AddVisitEvidenceCommandHandler : IRequestHandler<AddVisitEvidenceCommand, VisitEvidenceDto>
    {
        private readonly IVisitRepository _visitRepository;
        private readonly IEvidenceTypeRepository _evidenceTypeRepository;
        private readonly IEvidenceFieldValidator _fieldValidator;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITaskRepository? _taskRepository;
        private readonly IClientSiteRepository? _clientSiteRepository;

        public AddVisitEvidenceCommandHandler(
            IVisitRepository visitRepository,
            IEvidenceTypeRepository evidenceTypeRepository,
            ICurrentUserService currentUserService)
            : this(visitRepository, evidenceTypeRepository, new FFM.FieldOperations.Application.Services.EvidenceFieldValidator(), currentUserService, null, null)
        {
        }

        public AddVisitEvidenceCommandHandler(
            IVisitRepository visitRepository,
            IEvidenceTypeRepository evidenceTypeRepository,
            IEvidenceFieldValidator fieldValidator,
            ICurrentUserService currentUserService,
            ITaskRepository? taskRepository = null,
            IClientSiteRepository? clientSiteRepository = null)
        {
            _visitRepository = visitRepository;
            _evidenceTypeRepository = evidenceTypeRepository;
            _fieldValidator = fieldValidator;
            _currentUserService = currentUserService;
            _taskRepository = taskRepository;
            _clientSiteRepository = clientSiteRepository;
        }

        public async Task<VisitEvidenceDto> Handle(AddVisitEvidenceCommand request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // 1. Visit ID or Task ID Validation
            if (request.VisitId <= 0 && (!request.TaskId.HasValue || request.TaskId.Value <= 0))
                throw new ArgumentException("Valid VisitId or TaskId is required.", nameof(request.VisitId));

            // 2. Resolve & Validate Evidence Type
            int resolvedTypeId = request.EvidenceTypeId;
            string resolvedTypeCode = string.Empty;

            if (resolvedTypeId > 0)
            {
                var masterType = await _evidenceTypeRepository.GetByIdAsync(resolvedTypeId, cancellationToken);
                if (masterType != null && masterType.IsActive)
                {
                    resolvedTypeCode = masterType.Code;
                }
                else
                {
                    resolvedTypeCode = resolvedTypeId switch
                    {
                        1 => "PHOTO",
                        2 => "METER_READING",
                        3 => "SIGNATURE",
                        4 => "DOCUMENT",
                        5 => "INVOICE",
                        _ => "PHOTO"
                    };
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.EvidenceType))
            {
                var normalized = request.EvidenceType.Trim().ToUpperInvariant();
                var masterType = await _evidenceTypeRepository.GetByCodeAsync(normalized, cancellationToken);
                if (masterType != null && masterType.IsActive)
                {
                    resolvedTypeId = masterType.Id;
                    resolvedTypeCode = masterType.Code;
                }
                else if (VisitEvidence.AllowedEvidenceTypes.Contains(normalized))
                {
                    resolvedTypeCode = normalized;
                    resolvedTypeId = normalized switch
                    {
                        "PHOTO" => 1,
                        "METER_READING" => 2,
                        "SIGNATURE" => 3,
                        "DOCUMENT" => 4,
                        "INVOICE" => 5,
                        _ => 1
                    };
                }
                else
                {
                    throw new ArgumentException(
                        $"Evidence type '{request.EvidenceType}' is invalid. Allowed: {string.Join(", ", VisitEvidence.AllowedEvidenceTypes)}.",
                        nameof(request.EvidenceType));
                }
            }
            else
            {
                resolvedTypeId = 1;
                resolvedTypeCode = "PHOTO";
            }

            // 3. Resolve Target Files
            var targetFiles = new List<EvidenceFileItemDto>();

            if (request.Files != null && request.Files.Count > 0)
            {
                foreach (var f in request.Files)
                {
                    targetFiles.Add(new EvidenceFileItemDto
                    {
                        FileName = f.FileName ?? request.FileName,
                        FolderPath = f.FolderPath ?? request.FolderPath,
                        FileUrl = f.FileUrl?.Trim() ?? string.Empty,
                        EvidenceTypeId = f.EvidenceTypeId > 0 ? f.EvidenceTypeId : resolvedTypeId,
                        ThumbnailUrl = f.ThumbnailUrl ?? request.ThumbnailUrl,
                        Remarks = f.Remarks ?? request.Remarks
                    });
                }
            }
            else if (request.FileUrls != null && request.FileUrls.Count > 0)
            {
                foreach (var url in request.FileUrls)
                {
                    targetFiles.Add(new EvidenceFileItemDto
                    {
                        FileName = request.FileName,
                        FolderPath = request.FolderPath,
                        FileUrl = url?.Trim() ?? string.Empty,
                        EvidenceTypeId = resolvedTypeId,
                        ThumbnailUrl = request.ThumbnailUrl,
                        Remarks = request.Remarks
                    });
                }
            }
            else
            {
                targetFiles.Add(new EvidenceFileItemDto
                {
                    FileName = request.FileName,
                    FolderPath = request.FolderPath,
                    FileUrl = request.FileUrl?.Trim() ?? string.Empty,
                    EvidenceTypeId = resolvedTypeId,
                    ThumbnailUrl = request.ThumbnailUrl,
                    Remarks = request.Remarks
                });
            }

            if (!targetFiles.Any())
            {
                throw new ArgumentException("At least one FileUrl must be provided.", nameof(request.FileUrl));
            }

            // 4. Validate Each File URL
            foreach (var f in targetFiles)
            {
                if (string.IsNullOrWhiteSpace(f.FileUrl))
                {
                    throw new ArgumentException("FileUrl cannot be empty.", nameof(request.FileUrl));
                }

                if (f.FileUrl.Length > 500)
                {
                    throw new ArgumentException("Each FileUrl cannot exceed 500 characters.", nameof(request.FileUrl));
                }

                if (!Uri.TryCreate(f.FileUrl, UriKind.Absolute, out var fileUri) ||
                    (fileUri.Scheme != Uri.UriSchemeHttps && fileUri.Scheme != Uri.UriSchemeHttp))
                {
                    throw new ArgumentException($"FileUrl '{f.FileUrl}' must be a valid absolute HTTP or HTTPS URL.", nameof(request.FileUrl));
                }
            }

            // 5. Thumbnail URL Validation
            if (!string.IsNullOrWhiteSpace(request.ThumbnailUrl))
            {
                var trimmedThumbUrl = request.ThumbnailUrl.Trim();
                if (trimmedThumbUrl.Length > 500)
                    throw new ArgumentException("ThumbnailUrl cannot exceed 500 characters.", nameof(request.ThumbnailUrl));

                if (!Uri.TryCreate(trimmedThumbUrl, UriKind.Absolute, out var thumbUri) ||
                    (thumbUri.Scheme != Uri.UriSchemeHttps && thumbUri.Scheme != Uri.UriSchemeHttp))
                {
                    throw new ArgumentException("ThumbnailUrl must be a valid absolute HTTP or HTTPS URL.", nameof(request.ThumbnailUrl));
                }
            }

            // 6. Remarks Validation
            if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Trim().Length > 500)
            {
                throw new ArgumentException("Remarks cannot exceed 500 characters.", nameof(request.Remarks));
            }

            // 7. Coordinates Validation
            if (request.CapturedLatitude.HasValue && (request.CapturedLatitude < -90m || request.CapturedLatitude > 90m))
                throw new ArgumentException("CapturedLatitude must be between -90 and 90.", nameof(request.CapturedLatitude));

            if (request.CapturedLongitude.HasValue && (request.CapturedLongitude < -180m || request.CapturedLongitude > 180m))
                throw new ArgumentException("CapturedLongitude must be between -180 and 180.", nameof(request.CapturedLongitude));

            // 8. Dynamic Field-by-Field Validation Engine (BR-EV-VALIDATE)
            var validationResult = _fieldValidator.Validate(resolvedTypeId, resolvedTypeCode, request.Readings);
            if (!validationResult.IsValid)
            {
                var firstError = validationResult.Errors.First();
                throw new ArgumentException(firstError.Message, firstError.Field);
            }

            string? readingsJson = null;
            if (request.Readings != null && request.Readings.Count > 0)
            {
                readingsJson = System.Text.Json.JsonSerializer.Serialize(request.Readings);
            }

            // 9. Role / Caller Identification
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

            // 10. Task Geofence Validation & Automatic Visit Resolution (BR-ZERO-CHECKIN)
            Visit? visit = null;
            TaskItem? task = null;

            int? targetTaskId = request.TaskId;

            if (request.VisitId > 0)
            {
                visit = await _visitRepository.GetByIdAsync(request.VisitId, cancellationToken);
                if (visit != null && !targetTaskId.HasValue)
                {
                    targetTaskId = visit.TaskId;
                }
            }

            if (targetTaskId.HasValue && targetTaskId.Value > 0 && _taskRepository != null)
            {
                task = await _taskRepository.GetByIdAsync(targetTaskId.Value, cancellationToken);
                if (task != null)
                {
                    // Validate GPS directly against Task's ClientSite Geofence
                    ClientSite? site = task.ClientSite;
                    if (site == null && _clientSiteRepository != null)
                    {
                        site = await _clientSiteRepository.GetByIdAsync(task.ClientSiteId);
                    }

                    if (site != null && request.CapturedLatitude.HasValue && request.CapturedLongitude.HasValue)
                    {
                        var distanceMeters = CalculateHaversineDistanceMeters(
                            (double)request.CapturedLatitude.Value,
                            (double)request.CapturedLongitude.Value,
                            (double)site.Latitude,
                            (double)site.Longitude);

                        if (distanceMeters > site.RadiusMeters)
                        {
                            throw new InvalidOperationException(
                                $"Captured GPS coordinates are {distanceMeters:F1} meters away from client site '{site.Name}', exceeding the {site.RadiusMeters}m geofence boundary.");
                        }
                    }

                    // Auto-resolve or implicitly create Visit if not yet existing
                    if (visit == null)
                    {
                        visit = await _visitRepository.GetByTaskIdAsync(task.Id, cancellationToken);
                        if (visit == null)
                        {
                            var lat = request.CapturedLatitude ?? (site?.Latitude ?? 0m);
                            var lon = request.CapturedLongitude ?? (site?.Longitude ?? 0m);
                            visit = new Visit(
                                taskId: task.Id,
                                employeeId: task.AssignedEmployeeId ?? callerUserId,
                                checkInTime: DateTime.UtcNow,
                                checkInLatitude: lat,
                                checkInLongitude: lon,
                                checkInDistanceMeters: 0m,
                                isWithinGeofence: true);

                            visit = await _visitRepository.AddVisitAsync(visit, cancellationToken);
                        }
                    }
                }
            }

            if (visit == null && request.VisitId > 0)
            {
                visit = await _visitRepository.GetByIdAsync(request.VisitId, cancellationToken);
            }

            if (visit == null)
            {
                var idText = request.VisitId > 0 ? $"ID {request.VisitId}" : $"Task ID {request.TaskId}";
                throw new KeyNotFoundException($"Visit with {idText} was not found.");
            }

            int resolvedVisitId = visit.Id;

            // 11. Visit Status Guard
            if (visit.Status == "ABORTED" || visit.Status == "CANCELLED")
            {
                throw new InvalidOperationException($"Cannot attach evidence to a visit in '{visit.Status}' status.");
            }

            // 12. BOLA / Role Guard
            if (!isManagement)
            {
                bool isEmployee = callerRoles.Any(r => r.Equals("EMPLOYEE", StringComparison.OrdinalIgnoreCase));
                if (!isEmployee)
                {
                    throw new UnauthorizedAccessException("Access denied: Caller does not possess an authorized role to upload visit evidence.");
                }

                if (visit.EmployeeId != callerUserId)
                {
                    throw new UnauthorizedAccessException($"Access denied: You are not authorized to upload evidence for this visit. Caller ID {callerUserId} does not match assigned technician ID {visit.EmployeeId}.");
                }
            }

            // 13. Instantiate & Persist All Evidence Records
            var createdDtos = new List<VisitEvidenceDto>();
            var evidencesToSave = new List<VisitEvidence>();

            foreach (var f in targetFiles)
            {
                var masterType = await _evidenceTypeRepository.GetByIdAsync(f.EvidenceTypeId, cancellationToken);
                var fileTypeCode = masterType?.Code ?? (f.EvidenceTypeId switch
                {
                    1 => "PHOTO",
                    2 => "METER_READING",
                    3 => "SIGNATURE",
                    4 => "DOCUMENT",
                    5 => "INVOICE",
                    _ => resolvedTypeCode
                });

                var evidence = new VisitEvidence(
                    resolvedVisitId,
                    f.EvidenceTypeId,
                    f.FileUrl,
                    f.ThumbnailUrl?.Trim(),
                    f.Remarks?.Trim(),
                    request.CapturedLatitude,
                    request.CapturedLongitude,
                    DateTime.UtcNow,
                    fileTypeCode,
                    readingsJson,
                    f.FileName?.Trim(),
                    f.FolderPath?.Trim());

                visit.AddEvidence(evidence);
                evidencesToSave.Add(evidence);
            }

            // High-performance batch persist (1 network round-trip in production with fallback for mock compatibility)
            IReadOnlyList<VisitEvidence>? savedList = null;
            try
            {
                savedList = await _visitRepository.AddEvidencesBatchAsync(evidencesToSave, cancellationToken);
            }
            catch
            {
                savedList = null;
            }

            if (savedList == null)
            {
                var list = new List<VisitEvidence>();
                foreach (var ev in evidencesToSave)
                {
                    list.Add(await _visitRepository.AddEvidenceAsync(ev, cancellationToken));
                }
                savedList = list;
            }

            foreach (var saved in savedList)
            {
                createdDtos.Add(new VisitEvidenceDto
                {
                    Id = saved.Id,
                    VisitId = saved.VisitId,
                    EvidenceTypeId = saved.EvidenceTypeId,
                    EvidenceType = saved.EvidenceType,
                    FileName = saved.FileName,
                    FolderPath = saved.FolderPath,
                    FileUrl = saved.FileUrl,
                    FileUrls = new List<string> { saved.FileUrl },
                    ThumbnailUrl = saved.ThumbnailUrl,
                    Remarks = saved.Remarks,
                    CapturedLatitude = saved.CapturedLatitude,
                    CapturedLongitude = saved.CapturedLongitude,
                    CapturedAt = saved.CapturedAt,
                    Readings = request.Readings
                });
            }

            var first = createdDtos.FirstOrDefault() ?? throw new InvalidOperationException("Failed to persist visit evidence.");

            return new VisitEvidenceDto
            {
                Id = first.Id,
                VisitId = first.VisitId,
                EvidenceTypeId = first.EvidenceTypeId,
                EvidenceType = first.EvidenceType,
                FileName = first.FileName,
                FolderPath = first.FolderPath,
                FileUrl = first.FileUrl,
                FileUrls = targetFiles.Select(tf => tf.FileUrl).ToList(),
                Files = targetFiles,
                ThumbnailUrl = first.ThumbnailUrl,
                Remarks = first.Remarks,
                CapturedLatitude = first.CapturedLatitude,
                CapturedLongitude = first.CapturedLongitude,
                CapturedAt = first.CapturedAt,
                Readings = request.Readings,
                Items = createdDtos
            };
        }

        private static double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth radius in meters
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
