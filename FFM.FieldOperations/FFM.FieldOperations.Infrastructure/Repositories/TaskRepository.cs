using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using FFM.FieldOperations.Domain.Models;
using FFM.FieldOperations.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFM.FieldOperations.Infrastructure.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly FieldOperationsDbContext _context;

        public TaskRepository(FieldOperationsDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextTaskNumberAsync(CancellationToken cancellationToken = default)
        {
            var yearMonth = DateTime.UtcNow.ToString("yyyyMM");
            var prefix = $"TSK-{yearMonth}-";

            var lastTaskNumber = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.TaskNumber.StartsWith(prefix))
                .OrderByDescending(t => t.TaskNumber)
                .Select(t => t.TaskNumber)
                .FirstOrDefaultAsync(cancellationToken);

            int nextSequence = 1;
            if (!string.IsNullOrEmpty(lastTaskNumber) && lastTaskNumber.Length >= prefix.Length + 4)
            {
                var suffix = lastTaskNumber.Substring(prefix.Length);
                if (int.TryParse(suffix, out var parsed))
                {
                    nextSequence = parsed + 1;
                }
            }
            else
            {
                var countForMonth = await _context.Tasks
                    .AsNoTracking()
                    .CountAsync(t => t.TaskNumber.StartsWith(prefix), cancellationToken);
                nextSequence = countForMonth + 1;
            }

            return $"{prefix}{nextSequence:D4}";
        }

        public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            await _context.Tasks.AddAsync(task, cancellationToken);
        }

        public async Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .Include(t => t.ClientSite)
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        public async Task<TaskItem?> GetByTaskNumberAsync(string taskNumber, CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .Include(t => t.ClientSite)
                .FirstOrDefaultAsync(t => t.TaskNumber == taskNumber, cancellationToken);
        }

        public async Task<(IReadOnlyList<TaskItem> Items, int TotalCount)> GetPagedTasksAsync(
            TaskFilterParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Tasks
                .AsNoTracking()
                .AsQueryable();

            if (parameters.EmployeeId.HasValue)
            {
                query = query.Where(t => t.AssignedEmployeeId == parameters.EmployeeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameters.Status))
            {
                var status = parameters.Status.Trim().ToUpperInvariant();
                query = query.Where(t => t.Status == status);
            }

            if (parameters.ClientSiteId.HasValue)
            {
                query = query.Where(t => t.ClientSiteId == parameters.ClientSiteId.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameters.Priority))
            {
                var priority = parameters.Priority.Trim().ToUpperInvariant();
                query = query.Where(t => t.Priority == priority);
            }

            if (parameters.StartDate.HasValue)
            {
                var startUtc = parameters.StartDate.Value.Kind == DateTimeKind.Utc
                    ? parameters.StartDate.Value
                    : DateTime.SpecifyKind(parameters.StartDate.Value, DateTimeKind.Utc);
                query = query.Where(t => t.ScheduledStart >= startUtc);
            }

            if (parameters.EndDate.HasValue)
            {
                var endUtc = parameters.EndDate.Value.Kind == DateTimeKind.Utc
                    ? parameters.EndDate.Value
                    : DateTime.SpecifyKind(parameters.EndDate.Value, DateTimeKind.Utc);
                query = query.Where(t => t.ScheduledEnd <= endUtc);
            }

            if (!string.IsNullOrWhiteSpace(parameters.Search))
            {
                var term = parameters.Search.Trim().ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(term) ||
                    t.TaskNumber.ToLower().Contains(term) ||
                    (t.ClientSite != null && t.ClientSite.Name.ToLower().Contains(term)));
            }

            // High performance: count query without redundant table joins
            var totalCount = await query.CountAsync(cancellationToken);

            var sortBy = parameters.SortBy?.Trim().ToLowerInvariant();
            var isDesc = string.Equals(parameters.SortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

            query = (sortBy, isDesc) switch
            {
                ("createdat", false) => query.OrderBy(t => t.CreatedAt),
                ("createdat", true) => query.OrderByDescending(t => t.CreatedAt),
                ("priority", false) => query.OrderBy(t => t.Priority),
                ("priority", true) => query.OrderByDescending(t => t.Priority),
                ("tasknumber", false) => query.OrderBy(t => t.TaskNumber),
                ("tasknumber", true) => query.OrderByDescending(t => t.TaskNumber),
                ("scheduledstart", true) => query.OrderByDescending(t => t.ScheduledStart),
                _ => query.OrderBy(t => t.ScheduledStart)
            };

            var page = parameters.Page < 1 ? 1 : parameters.Page;
            var pageSize = parameters.PageSize < 1 ? 20 : (parameters.PageSize > 100 ? 100 : parameters.PageSize);

            // Apply Include ONLY to the paged window
            var items = await query
                .Include(t => t.ClientSite)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
