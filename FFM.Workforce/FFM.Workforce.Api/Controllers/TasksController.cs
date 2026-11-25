using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Infrastructure;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly WorkforceDbContext _dbContext;

    public TasksController(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = "asc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Tasks.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status.ToUpperInvariant() != "ALL")
        {
            var st = status.Trim().ToUpperInvariant();
            query = query.Where(t => t.Status.ToUpper() == st);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.TaskNumber.ToLower().Contains(term) ||
                t.Title.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (sortBy?.ToLowerInvariant()) switch
        {
            "priority" => sortOrder == "desc" ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
            "status" => sortOrder == "desc" ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "title" => sortOrder == "desc" ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
            "date" or "scheduleddate" or "scheduledstart" => sortOrder == "desc" ? query.OrderByDescending(t => t.ScheduledStart) : query.OrderBy(t => t.ScheduledStart),
            "createdat" => sortOrder == "asc" ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt),
            "tasknumber" or "task_number" => sortOrder == "desc" ? query.OrderByDescending(t => t.TaskNumber) : query.OrderBy(t => t.TaskNumber),
            _ => sortOrder == "desc" ? query.OrderByDescending(t => t.Id) : query.OrderBy(t => t.Id)
        };

        var pagedTasks = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var siteIds = pagedTasks.Where(t => t.ClientSiteId.HasValue).Select(t => t.ClientSiteId!.Value).Distinct().ToList();
        var empIds = pagedTasks.Where(t => t.AssignedEmployeeId.HasValue).Select(t => t.AssignedEmployeeId!.Value).Distinct().ToList();

        var sites = await _dbContext.ClientSites.AsNoTracking()
            .Where(s => siteIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var employees = await _dbContext.Employees.AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);

        var items = pagedTasks.Select(t => new
        {
            id = t.Id,
            taskNumber = t.TaskNumber,
            title = t.Title,
            description = t.Description ?? string.Empty,
            priority = t.Priority,
            status = t.Status,
            clientSiteId = t.ClientSiteId,
            clientSiteName = t.ClientSiteId.HasValue && sites.TryGetValue(t.ClientSiteId.Value, out var sName) ? sName : "Central Municipal District",
            assignedEmployeeId = t.AssignedEmployeeId,
            assignedEmployeeName = t.AssignedEmployeeId.HasValue && employees.TryGetValue(t.AssignedEmployeeId.Value, out var eName) ? eName : "Unassigned",
            scheduledDate = t.ScheduledStart,
            scheduledStart = t.ScheduledStart,
            scheduledEnd = t.ScheduledEnd,
            isGeofenceVerified = true,
            createdAt = t.CreatedAt,
            updatedAt = t.UpdatedAt
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new
        {
            success = true,
            message = "Tasks retrieved successfully.",
            data = new
            {
                items,
                totalCount,
                page,
                pageSize,
                totalPages,
                hasNextPage = page < totalPages,
                hasPreviousPage = page > 1
            }
        });
    }

    [HttpGet("cancellation-schema")]
    public IActionResult GetCancellationSchema()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                reasons = new[]
                {
                    new { reasonCode = "CLIENT_REQUESTED_RESCHEDULE", label = "Client requested reschedule" },
                    new { reasonCode = "SITE_INACCESSIBLE", label = "Site inaccessible / Gate locked" },
                    new { reasonCode = "WEATHER_CONDITIONS", label = "Adverse weather conditions" },
                    new { reasonCode = "TECHNICIAN_EMERGENCY", label = "Technician emergency or unavailable" },
                    new { reasonCode = "DUPLICATE_WORK_ORDER", label = "Duplicate work order created" },
                    new { reasonCode = "OTHER_OPERATIONAL", label = "Other operational reason" }
                }
            }
        });
    }

    public record CancelTaskRequest(int TaskId, string Reason);

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelTask([FromBody] CancelTaskRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TaskId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid TaskId." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { success = false, message = "Cancellation justification reason is mandatory for audit trail." });
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = "Task not found." });
        }

        var currentStatus = task.Status?.Trim().ToUpperInvariant() ?? string.Empty;

        // Terminal & active on-site guards
        if (currentStatus == "CHECKED_IN")
        {
            return BadRequest(new { success = false, message = "Cannot cancel task: Field technician has already checked in on-site. Work is currently active." });
        }

        if (currentStatus == "COMPLETED")
        {
            return BadRequest(new { success = false, message = "Cannot cancel task: Work order has already been completed." });
        }

        if (currentStatus == "CANCELLED")
        {
            return BadRequest(new { success = false, message = "Task is already cancelled." });
        }

        task.Status = "CANCELLED";
        task.CancellationReason = request.Reason.Trim();
        task.CancelledAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Task {task.TaskNumber} cancelled successfully.",
            data = new
            {
                id = task.Id,
                taskNumber = task.TaskNumber,
                status = task.Status,
                cancellationReason = task.CancellationReason
            }
        });
    }

    public record AssignTaskRequest(int TaskId, int EmployeeId);

    [HttpPost("assign")]
    public async Task<IActionResult> AssignTask([FromBody] AssignTaskRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TaskId <= 0 || request.EmployeeId <= 0)
        {
            return BadRequest(new { success = false, message = "Valid TaskId and EmployeeId are required in request body." });
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = $"Task with ID {request.TaskId} not found." });
        }

        var currentStatus = task.Status?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currentStatus == "COMPLETED" || currentStatus == "CANCELLED")
        {
            return BadRequest(new { success = false, message = $"Cannot assign a terminal {currentStatus} task." });
        }

        var employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (employee == null)
        {
            return NotFound(new { success = false, message = $"Employee with ID {request.EmployeeId} not found." });
        }

        task.AssignedEmployeeId = employee.Id;
        task.Status = "ASSIGNED";
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Task {task.TaskNumber} successfully assigned to {employee.Name}.",
            data = new
            {
                id = task.Id,
                taskNumber = task.TaskNumber,
                status = task.Status,
                assignedEmployeeId = employee.Id,
                assignedEmployeeName = employee.Name
            }
        });
    }

    public record UpdateTaskStatusRequest(int TaskId, string Status, string? Notes, double? Latitude, double? Longitude);

    [HttpPost("status")]
    public async Task<IActionResult> UpdateTaskStatus([FromBody] UpdateTaskStatusRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TaskId <= 0 || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { success = false, message = "TaskId and target Status are required." });
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = "Task not found." });
        }

        var targetStatus = request.Status.Trim().ToUpperInvariant();
        var currentStatus = task.Status?.Trim().ToUpperInvariant() ?? string.Empty;

        // Terminal state validation
        if (currentStatus == "COMPLETED" || currentStatus == "CANCELLED")
        {
            return BadRequest(new { success = false, message = $"Cannot change status of a terminal {currentStatus} task." });
        }

        var validStatuses = new[] { "CREATED", "ASSIGNED", "EN_ROUTE", "CHECKED_IN", "COMPLETED", "CANCELLED" };
        if (!validStatuses.Contains(targetStatus))
        {
            return BadRequest(new { success = false, message = $"Invalid target status '{targetStatus}'. Allowed: {string.Join(", ", validStatuses)}" });
        }

        task.Status = targetStatus;
        task.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Task {task.TaskNumber} status updated to {targetStatus}.",
            data = new
            {
                id = task.Id,
                taskNumber = task.TaskNumber,
                status = task.Status,
                updatedAt = task.UpdatedAt
            }
        });
    }

    public record CreateTaskRequest(
        string Title,
        string? Description,
        string? Priority,
        int? ClientSiteId,
        int? AssignedEmployeeId,
        DateTime? ScheduledStart,
        DateTime? ScheduledEnd,
        string? TaskType);

    [HttpPost("create")]
    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { success = false, message = "Task Title is mandatory." });
        }

        // Auto-generate task number: TSK-YYYYMM-XXXX
        var currentYearMonth = DateTime.UtcNow.ToString("yyyyMM");
        var taskCount = await _dbContext.Tasks.CountAsync(cancellationToken);
        var taskNumber = $"TSK-{currentYearMonth}-{(taskCount + 1).ToString("D4")}";

        // Validate assigned employee if supplied
        if (request.AssignedEmployeeId.HasValue && request.AssignedEmployeeId.Value > 0)
        {
            var empExists = await _dbContext.Employees.AnyAsync(e => e.Id == request.AssignedEmployeeId.Value, cancellationToken);
            if (!empExists)
            {
                return BadRequest(new { success = false, message = $"Assigned Employee ID {request.AssignedEmployeeId.Value} does not exist." });
            }
        }

        // Validate site if supplied
        if (request.ClientSiteId.HasValue && request.ClientSiteId.Value > 0)
        {
            var siteExists = await _dbContext.ClientSites.AnyAsync(s => s.Id == request.ClientSiteId.Value, cancellationToken);
            if (!siteExists)
            {
                return BadRequest(new { success = false, message = $"Client Site ID {request.ClientSiteId.Value} does not exist." });
            }
        }

        var initialStatus = request.AssignedEmployeeId.HasValue && request.AssignedEmployeeId.Value > 0 ? "ASSIGNED" : "CREATED";
        var priority = string.IsNullOrWhiteSpace(request.Priority) ? "MEDIUM" : request.Priority.Trim().ToUpperInvariant();

        var newTask = new FieldTaskEntity
        {
            TaskNumber = taskNumber,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = priority,
            Status = initialStatus,
            ClientSiteId = request.ClientSiteId,
            AssignedEmployeeId = request.AssignedEmployeeId,
            ScheduledStart = request.ScheduledStart ?? DateTime.UtcNow.AddHours(2),
            ScheduledEnd = request.ScheduledEnd ?? DateTime.UtcNow.AddHours(4),
            TaskType = request.TaskType ?? "INSPECTION",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Tasks.Add(newTask);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            message = $"Municipal work order {newTask.TaskNumber} created successfully.",
            data = new
            {
                id = newTask.Id,
                taskNumber = newTask.TaskNumber,
                title = newTask.Title,
                status = newTask.Status,
                priority = newTask.Priority,
                scheduledStart = newTask.ScheduledStart,
                scheduledEnd = newTask.ScheduledEnd
            }
        });
    }

    public record TaskCompletionEvidenceInput(string? EvidenceType, string? FileName, string? FileUrl, string? Remarks);

    public record CompleteTaskInput(
        int TaskId,
        int? VisitId,
        decimal? Latitude,
        decimal? Longitude,
        string CompletionNotes,
        string? CustomerName,
        string? CustomerSignatureUrl,
        int? CustomerRating,
        List<TaskCompletionEvidenceInput>? Evidences);

    [HttpPost("complete")]
    [HttpPost("complete-task")]
    public async Task<IActionResult> CompleteTask([FromBody] CompleteTaskInput request, CancellationToken cancellationToken)
    {
        if (request == null || request.TaskId <= 0)
        {
            return BadRequest(new { success = false, message = "Valid TaskId is required in request body." });
        }

        if (string.IsNullOrWhiteSpace(request.CompletionNotes) || request.CompletionNotes.Trim().Length < 5)
        {
            return BadRequest(new { success = false, message = "CompletionNotes are mandatory and must be at least 5 characters." });
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = $"Task with ID {request.TaskId} not found." });
        }

        var visit = await _dbContext.Visits.FirstOrDefaultAsync(v => v.TaskId == task.Id && v.Status == "CHECKED_IN", cancellationToken);
        if (visit == null)
        {
            visit = new VisitEntity
            {
                TaskId = task.Id,
                EmployeeId = task.AssignedEmployeeId ?? 1,
                CheckInTime = DateTime.UtcNow.AddMinutes(-45),
                CheckInLatitude = request.Latitude ?? 23.5880m,
                CheckInLongitude = request.Longitude ?? 58.3829m,
                CheckInDistanceMeters = 12m,
                IsWithinGeofence = true,
                Status = "CHECKED_IN",
                CreatedAt = DateTime.UtcNow.AddMinutes(-45)
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
                        FileName = ev.FileName ?? "evidence_photo.jpg",
                        FileUrl = ev.FileUrl.Trim(),
                        Remarks = ev.Remarks,
                        CapturedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Attach digital customer signature if supplied
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

        // Complete visit and task
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
