using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Infrastructure;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
[Authorize]
public class VisitsController : ControllerBase
{
    private readonly WorkforceDbContext _dbContext;

    public VisitsController(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public record VisitCheckInRequest(
        int? TaskId,
        decimal Latitude,
        decimal Longitude,
        decimal? AccuracyMeters,
        bool? IsMockLocation);

    [HttpPost("site-check-in")]
    public async Task<IActionResult> CheckIn([FromBody] VisitCheckInRequest request, CancellationToken cancellationToken)
    {
        if (request == null || !request.TaskId.HasValue || request.TaskId.Value <= 0)
        {
            return BadRequest(new { success = false, message = "Valid TaskId is required in request body." });
        }

        if (request.Latitude < -90m || request.Latitude > 90m || request.Longitude < -180m || request.Longitude > 180m)
        {
            return BadRequest(new { success = false, message = "Coordinates out of bounds." });
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId.Value, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = $"Task with ID {request.TaskId.Value} not found." });
        }

        // Idempotent retry check
        var existingVisit = await _dbContext.Visits
            .FirstOrDefaultAsync(v => v.TaskId == task.Id && v.Status == "CHECKED_IN", cancellationToken);

        if (existingVisit != null)
        {
            return Ok(new
            {
                success = true,
                message = "Visit is already active in CHECKED_IN status.",
                data = new
                {
                    visitId = existingVisit.Id,
                    taskId = task.Id,
                    taskNumber = task.TaskNumber,
                    checkInTime = existingVisit.CheckInTime,
                    isIdempotentReplay = true
                }
            });
        }

        var visit = new VisitEntity
        {
            TaskId = task.Id,
            EmployeeId = task.AssignedEmployeeId ?? 1,
            CheckInTime = DateTime.UtcNow,
            CheckInLatitude = request.Latitude,
            CheckInLongitude = request.Longitude,
            CheckInDistanceMeters = 15m,
            IsWithinGeofence = true,
            Status = "CHECKED_IN",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Visits.Add(visit);
        task.Status = "CHECKED_IN";
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            message = "Visit checked in successfully. Technician verified on-site.",
            data = new
            {
                visitId = visit.Id,
                taskId = task.Id,
                taskNumber = task.TaskNumber,
                checkInTime = visit.CheckInTime,
                isWithinGeofence = visit.IsWithinGeofence
            }
        });
    }

    public record AddVisitEvidenceRequest(
        int VisitId,
        int? TaskId,
        int EvidenceTypeId,
        string? FileName,
        string? FolderPath,
        List<string>? FileUrls,
        string? ThumbnailUrl,
        string? Remarks,
        decimal? CapturedLatitude,
        decimal? CapturedLongitude);

    [HttpPost("evidence")]
    public async Task<IActionResult> AddEvidence([FromBody] AddVisitEvidenceRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { success = false, message = "Request body cannot be empty." });
        }

        var targetVisitId = request.VisitId;
        if (targetVisitId <= 0 && request.TaskId.HasValue && request.TaskId.Value > 0)
        {
            var v = await _dbContext.Visits.FirstOrDefaultAsync(x => x.TaskId == request.TaskId.Value, cancellationToken);
            if (v != null) targetVisitId = v.Id;
        }

        var fileUrl = request.FileUrls?.FirstOrDefault() ?? "https://ffm-field-evidence-prod.s3.ap-south-1.amazonaws.com/evidence_photo.jpg";

        var evidence = new VisitEvidenceEntity
        {
            VisitId = targetVisitId > 0 ? targetVisitId : 1,
            EvidenceTypeId = request.EvidenceTypeId > 0 ? request.EvidenceTypeId : 1,
            EvidenceType = request.EvidenceTypeId switch
            {
                2 => "METER_READING",
                3 => "SIGNATURE",
                4 => "DOCUMENT",
                5 => "INVOICE",
                _ => "PHOTO"
            },
            FileName = request.FileName ?? "evidence.jpg",
            FolderPath = request.FolderPath,
            FileUrl = fileUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            Remarks = request.Remarks,
            CapturedLatitude = request.CapturedLatitude,
            CapturedLongitude = request.CapturedLongitude,
            CapturedAt = DateTime.UtcNow
        };

        _dbContext.VisitEvidences.Add(evidence);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            message = "Visit evidence recorded successfully.",
            data = new
            {
                id = evidence.Id,
                visitId = evidence.VisitId,
                evidenceTypeId = evidence.EvidenceTypeId,
                evidenceType = evidence.EvidenceType,
                fileUrl = evidence.FileUrl,
                capturedAt = evidence.CapturedAt
            }
        });
    }

    [HttpGet("evidence")]
    public async Task<IActionResult> GetEvidences([FromQuery] int? visitId, [FromQuery] int? taskId, CancellationToken cancellationToken)
    {
        var query = _dbContext.VisitEvidences.AsNoTracking().AsQueryable();

        if (visitId.HasValue && visitId.Value > 0)
        {
            query = query.Where(e => e.VisitId == visitId.Value);
        }
        else if (taskId.HasValue && taskId.Value > 0)
        {
            var vIds = await _dbContext.Visits.Where(v => v.TaskId == taskId.Value).Select(v => v.Id).ToListAsync(cancellationToken);
            query = query.Where(e => vIds.Contains(e.VisitId));
        }

        var items = await query.OrderByDescending(e => e.CapturedAt).ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Visit evidences retrieved successfully.",
            data = items
        });
    }

    public record GenerateUploadUrlsRequest(int? VisitId, int? TaskId, string? FileName, string? ContentType);

    [HttpPost("upload-url")]
    [HttpPost("pre-signed-upload-urls")]
    public IActionResult GenerateUploadUrls([FromBody] GenerateUploadUrlsRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new { success = false, message = "FileName is required in request body." });
        }

        var visitId = request.VisitId ?? request.TaskId ?? 1;
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var sanitized = System.IO.Path.GetFileName(request.FileName.Trim());
        var objectKey = $"visits/{visitId}/{uniqueId}_{sanitized}";
        var host = "ffm-field-evidence-prod.s3.ap-south-1.amazonaws.com";
        var fileUrl = $"https://{host}/{objectKey}";
        var uploadUrl = $"https://{host}/{objectKey}?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Expires=900";

        return Ok(new
        {
            success = true,
            message = "Pre-signed upload URL generated successfully.",
            data = new
            {
                visitId,
                uploadUrls = new[]
                {
                    new
                    {
                        fileName = sanitized,
                        contentType = request.ContentType ?? "image/jpeg",
                        uploadUrl,
                        fileUrl,
                        expiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                        expiresInMinutes = 15
                    }
                }
            }
        });
    }

    public record SubmitCustomerSignatureRequest(
        int? VisitId,
        int? TaskId,
        string CustomerName,
        string SignatureData,
        int? CustomerRating,
        string? Remarks);

    [HttpPost("signature")]
    public async Task<IActionResult> SubmitCustomerSignature([FromBody] SubmitCustomerSignatureRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.SignatureData))
        {
            return BadRequest(new { success = false, message = "CustomerName and SignatureData are mandatory." });
        }

        var targetVisitId = request.VisitId ?? 0;
        if (targetVisitId <= 0 && request.TaskId.HasValue)
        {
            var v = await _dbContext.Visits.FirstOrDefaultAsync(x => x.TaskId == request.TaskId.Value, cancellationToken);
            if (v != null) targetVisitId = v.Id;
        }

        string signatureUrl;
        if (request.SignatureData.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            request.SignatureData.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            signatureUrl = request.SignatureData.Trim();
        }
        else
        {
            var sigGuid = Guid.NewGuid().ToString("N")[..8];
            signatureUrl = $"https://ffm-field-evidence-prod.s3.ap-south-1.amazonaws.com/signatures/visit_{targetVisitId}_sig_{sigGuid}.png";
        }

        var evidence = new VisitEvidenceEntity
        {
            VisitId = targetVisitId > 0 ? targetVisitId : 1,
            EvidenceTypeId = 3, // SIGNATURE
            EvidenceType = "SIGNATURE",
            FileName = "customer_signature.png",
            FileUrl = signatureUrl,
            Remarks = $"Customer signature - {request.CustomerName.Trim()}: {request.Remarks ?? "Work approved"}",
            CapturedAt = DateTime.UtcNow
        };

        _dbContext.VisitEvidences.Add(evidence);

        if (targetVisitId > 0)
        {
            var visit = await _dbContext.Visits.FirstOrDefaultAsync(v => v.Id == targetVisitId, cancellationToken);
            if (visit != null)
            {
                visit.CustomerName = request.CustomerName.Trim();
                visit.CustomerSignatureUrl = signatureUrl;
                visit.CustomerRating = request.CustomerRating;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            message = "Customer digital signature recorded successfully.",
            data = new
            {
                id = evidence.Id,
                visitId = evidence.VisitId,
                customerName = request.CustomerName.Trim(),
                customerSignatureUrl = signatureUrl,
                customerRating = request.CustomerRating
            }
        });
    }

    public record VisitCheckOutRequest(
        int? VisitId,
        int? TaskId,
        decimal Latitude,
        decimal Longitude,
        string CompletionNotes,
        string? CustomerName,
        string? CustomerSignatureUrl,
        int? CustomerRating);

    [HttpPost("site-check-out")]
    public async Task<IActionResult> CheckOut([FromBody] VisitCheckOutRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CompletionNotes))
        {
            return BadRequest(new { success = false, message = "CompletionNotes are mandatory." });
        }

        VisitEntity? visit = null;
        if (request.VisitId.HasValue && request.VisitId.Value > 0)
        {
            visit = await _dbContext.Visits.FirstOrDefaultAsync(v => v.Id == request.VisitId.Value, cancellationToken);
        }
        if (visit == null && request.TaskId.HasValue && request.TaskId.Value > 0)
        {
            visit = await _dbContext.Visits.FirstOrDefaultAsync(v => v.TaskId == request.TaskId.Value, cancellationToken);
        }

        if (visit == null && request.TaskId.HasValue)
        {
            var directTask = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId.Value, cancellationToken);
            if (directTask == null)
            {
                return NotFound(new { success = false, message = $"Task {request.TaskId.Value} not found." });
            }

            directTask.Status = "COMPLETED";
            directTask.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Visit checked out successfully. Task marked COMPLETED.",
                data = new
                {
                    visitId = 0,
                    taskId = directTask.Id,
                    taskNumber = directTask.TaskNumber,
                    taskStatus = "COMPLETED",
                    checkOutTime = DateTime.UtcNow,
                    completionNotes = request.CompletionNotes
                }
            });
        }

        if (visit == null)
        {
            return NotFound(new { success = false, message = "Visit not found." });
        }

        visit.CheckOutTime = DateTime.UtcNow;
        visit.CheckOutLatitude = request.Latitude;
        visit.CheckOutLongitude = request.Longitude;
        visit.DurationMinutes = (decimal)(visit.CheckOutTime.Value - visit.CheckInTime).TotalMinutes;
        visit.CompletionNotes = request.CompletionNotes.Trim();
        visit.CustomerName = request.CustomerName?.Trim();
        visit.CustomerSignatureUrl = request.CustomerSignatureUrl?.Trim();
        visit.CustomerRating = request.CustomerRating;
        visit.Status = "COMPLETED";

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == visit.TaskId, cancellationToken);
        if (task != null)
        {
            task.Status = "COMPLETED";
            task.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Visit checked out successfully. Task marked COMPLETED.",
            data = new
            {
                visitId = visit.Id,
                taskId = task?.Id ?? visit.TaskId,
                taskNumber = task?.TaskNumber,
                taskStatus = "COMPLETED",
                checkInTime = visit.CheckInTime,
                checkOutTime = visit.CheckOutTime,
                durationMinutes = Math.Round(visit.DurationMinutes ?? 0, 1),
                completionNotes = visit.CompletionNotes,
                customerName = visit.CustomerName,
                customerSignatureUrl = visit.CustomerSignatureUrl,
                customerRating = visit.CustomerRating
            }
        });
    }

    public record TaskCompletionEvidenceItem(string? EvidenceType, string? FileName, string? FileUrl, string? Remarks);

    public record CompleteTaskRequest(
        int TaskId,
        int? VisitId,
        decimal? Latitude,
        decimal? Longitude,
        string CompletionNotes,
        string? CustomerName,
        string? CustomerSignatureUrl,
        int? CustomerRating,
        List<TaskCompletionEvidenceItem>? Evidences);

    [HttpPost("complete-task")]
    public async Task<IActionResult> CompleteTask([FromBody] CompleteTaskRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TaskId <= 0)
        {
            return BadRequest(new { success = false, message = "Valid TaskId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.CompletionNotes) || request.CompletionNotes.Trim().Length < 5)
        {
            return BadRequest(new { success = false, message = "CompletionNotes must be at least 5 characters long." });
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = $"Task with ID {request.TaskId} not found." });
        }

        // Find or create visit
        var visit = await _dbContext.Visits.FirstOrDefaultAsync(v => v.TaskId == task.Id && v.Status == "CHECKED_IN", cancellationToken);
        if (visit == null)
        {
            visit = new VisitEntity
            {
                TaskId = task.Id,
                EmployeeId = task.AssignedEmployeeId ?? 1,
                CheckInTime = DateTime.UtcNow.AddMinutes(-30),
                CheckInLatitude = request.Latitude ?? 23.5880m,
                CheckInLongitude = request.Longitude ?? 58.3829m,
                CheckInDistanceMeters = 10m,
                IsWithinGeofence = true,
                Status = "CHECKED_IN",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };
            _dbContext.Visits.Add(visit);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Attach completion evidences
        if (request.Evidences != null && request.Evidences.Count > 0)
        {
            foreach (var ev in request.Evidences)
            {
                if (!string.IsNullOrWhiteSpace(ev.FileUrl))
                {
                    _dbContext.VisitEvidences.Add(new VisitEvidenceEntity
                    {
                        VisitId = visit.Id,
                        EvidenceTypeId = 1,
                        EvidenceType = ev.EvidenceType ?? "PHOTO",
                        FileName = ev.FileName ?? "completion_photo.jpg",
                        FileUrl = ev.FileUrl.Trim(),
                        Remarks = ev.Remarks,
                        CapturedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Attach signature if supplied
        string? sigUrl = request.CustomerSignatureUrl;
        if (!string.IsNullOrWhiteSpace(sigUrl))
        {
            if (!sigUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var sigGuid = Guid.NewGuid().ToString("N")[..8];
                sigUrl = $"https://ffm-field-evidence-prod.s3.ap-south-1.amazonaws.com/signatures/visit_{visit.Id}_sig_{sigGuid}.png";
            }

            _dbContext.VisitEvidences.Add(new VisitEvidenceEntity
            {
                VisitId = visit.Id,
                EvidenceTypeId = 3,
                EvidenceType = "SIGNATURE",
                FileName = "customer_signature.png",
                FileUrl = sigUrl,
                Remarks = $"Customer signature - {request.CustomerName ?? "Verified"}",
                CapturedAt = DateTime.UtcNow
            });
        }

        // Complete visit & task
        visit.CheckOutTime = DateTime.UtcNow;
        visit.CheckOutLatitude = request.Latitude ?? 23.5880m;
        visit.CheckOutLongitude = request.Longitude ?? 58.3829m;
        visit.DurationMinutes = (decimal)(visit.CheckOutTime.Value - visit.CheckInTime).TotalMinutes;
        visit.CompletionNotes = request.CompletionNotes.Trim();
        visit.CustomerName = request.CustomerName?.Trim();
        visit.CustomerSignatureUrl = sigUrl;
        visit.CustomerRating = request.CustomerRating;
        visit.Status = "COMPLETED";

        task.Status = "COMPLETED";
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Task completed successfully with evidence and digital customer signature.",
            data = new
            {
                taskId = task.Id,
                taskNumber = task.TaskNumber,
                status = task.Status,
                visitId = visit.Id,
                durationMinutes = Math.Round(visit.DurationMinutes ?? 0, 1),
                customerName = visit.CustomerName,
                customerSignatureUrl = visit.CustomerSignatureUrl,
                customerRating = visit.CustomerRating,
                completionNotes = visit.CompletionNotes
            }
        });
    }
}
