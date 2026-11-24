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
            "date" or "scheduleddate" => sortOrder == "desc" ? query.OrderByDescending(t => t.ScheduledStart) : query.OrderBy(t => t.ScheduledStart),
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
            .ToDictionaryAsync(s => s.Id, s => s.SiteName, cancellationToken);

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

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task == null)
        {
            return NotFound(new { success = false, message = "Task not found." });
        }

        task.Status = "CANCELLED";
        task.CancellationReason = request.Reason;
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
}
