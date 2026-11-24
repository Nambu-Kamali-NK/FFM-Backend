using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Queries;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Domain.Models;
using MediatR;

namespace FFM.FieldOperations.Application.Handlers
{
    public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, PagedResult<TaskDto>>
    {
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "CREATED", "ASSIGNED", "EN_ROUTE", "CHECKED_IN", "COMPLETED", "CANCELLED"
        };

        private static readonly HashSet<string> ValidPriorities = new(StringComparer.OrdinalIgnoreCase)
        {
            "LOW", "MEDIUM", "HIGH", "URGENT"
        };

        private readonly ITaskRepository _taskRepository;

        public GetTasksQueryHandler(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<PagedResult<TaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
        {
            var filter = request.Filter ?? new GetTasksFilterQuery();

            // 1. Validate Pagination Boundaries
            if (filter.Page < 1)
            {
                throw new ArgumentException("Page must be at least 1.", nameof(filter.Page));
            }

            if (filter.PageSize < 1 || filter.PageSize > 100)
            {
                throw new ArgumentException("PageSize must be between 1 and 100.", nameof(filter.PageSize));
            }

            // 2. Validate Status Whitelist
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var normalizedStatus = filter.Status.Trim();
                if (!ValidStatuses.Contains(normalizedStatus))
                {
                    throw new ArgumentException($"Invalid status '{filter.Status}'. Valid statuses: CREATED, ASSIGNED, EN_ROUTE, CHECKED_IN, COMPLETED, CANCELLED.", nameof(filter.Status));
                }
            }

            // 3. Validate Priority Whitelist
            if (!string.IsNullOrWhiteSpace(filter.Priority))
            {
                var normalizedPriority = filter.Priority.Trim();
                if (!ValidPriorities.Contains(normalizedPriority))
                {
                    throw new ArgumentException($"Invalid priority '{filter.Priority}'. Valid priorities: LOW, MEDIUM, HIGH, URGENT.", nameof(filter.Priority));
                }
            }

            // 4. Validate Date Range Chronology
            if (filter.StartDate.HasValue && filter.EndDate.HasValue)
            {
                var startUtc = filter.StartDate.Value.Kind == DateTimeKind.Utc
                    ? filter.StartDate.Value
                    : DateTime.SpecifyKind(filter.StartDate.Value, DateTimeKind.Utc);

                var endUtc = filter.EndDate.Value.Kind == DateTimeKind.Utc
                    ? filter.EndDate.Value
                    : DateTime.SpecifyKind(filter.EndDate.Value, DateTimeKind.Utc);

                if (endUtc < startUtc)
                {
                    throw new ArgumentException("EndDate cannot be earlier than StartDate.", nameof(filter.EndDate));
                }
            }

            // 5. RBAC Self-Scoping for Field Technicians (Role: EMPLOYEE)
            var callerRoles = request.CallerRoles ?? Enumerable.Empty<string>();
            var isStrictlyEmployee = callerRoles.Contains("EMPLOYEE", StringComparer.OrdinalIgnoreCase) &&
                !callerRoles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(r, "SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(r, "MANAGER", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(r, "SUPERVISOR", StringComparison.OrdinalIgnoreCase));

            var effectiveEmployeeId = isStrictlyEmployee ? request.CallerUserId : filter.EmployeeId;

            // 6. Map to Domain Filter Parameters
            var filterParams = new TaskFilterParameters
            {
                EmployeeId = effectiveEmployeeId,
                Status = filter.Status,
                ClientSiteId = filter.ClientSiteId,
                Priority = filter.Priority,
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                Search = filter.Search,
                Page = filter.Page,
                PageSize = filter.PageSize,
                SortBy = filter.SortBy,
                SortOrder = filter.SortOrder
            };

            // 7. Query Data
            var (items, totalCount) = await _taskRepository.GetPagedTasksAsync(filterParams, cancellationToken);

            // 8. Map to DTOs
            var dtos = items.Select(t => new TaskDto
            {
                Id = t.Id,
                TaskNumber = t.TaskNumber,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                TaskType = t.TaskType,
                CustomData = string.IsNullOrWhiteSpace(t.CustomDataJson)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(t.CustomDataJson),
                ClientSiteId = t.ClientSiteId,
                ClientSiteName = t.ClientSite?.Name ?? string.Empty,
                ClientSiteCode = t.ClientSite?.SiteCode ?? string.Empty,
                Address = t.ClientSite?.Address ?? string.Empty,
                Latitude = t.ClientSite?.Latitude ?? 0m,
                Longitude = t.ClientSite?.Longitude ?? 0m,
                RadiusMeters = t.ClientSite?.RadiusMeters ?? 0,
                AssignedEmployeeId = t.AssignedEmployeeId,
                ScheduledStart = t.ScheduledStart,
                ScheduledEnd = t.ScheduledEnd,
                CreatedByUserId = t.CreatedByUserId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return new PagedResult<TaskDto>
            {
                Items = dtos,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }
    }
}
