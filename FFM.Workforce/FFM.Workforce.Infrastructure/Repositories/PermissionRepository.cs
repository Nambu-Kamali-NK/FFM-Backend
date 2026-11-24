using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Infrastructure.Repositories
{
    public class PermissionRepository : IPermissionRepository
    {
        private readonly WorkforceDbContext _dbContext;

        public PermissionRepository(WorkforceDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddPermissionRequestAsync(PermissionRequest request)
        {
            await _dbContext.PermissionRequests.AddAsync(request);
        }

        public async Task<PermissionRequest?> GetPermissionRequestByIdAsync(int id)
        {
            if (id <= 0) return null;
            return await _dbContext.PermissionRequests.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<bool> HasOverlappingPermissionAsync(
            int employeeId,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            int? excludeRequestId = null)
        {
            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var query = _dbContext.PermissionRequests
                .Where(p => p.EmployeeId == employeeId
                            && (p.Status == LeaveStatus.PENDING || p.Status == LeaveStatus.APPROVED)
                            && p.PermissionDate == targetDate
                            && p.StartTime < endTime
                            && p.EndTime > startTime);

            if (excludeRequestId.HasValue)
            {
                query = query.Where(p => p.Id != excludeRequestId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> HasOverlappingLeaveOnDateAsync(int employeeId, DateTime date)
        {
            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            return await _dbContext.LeaveRequests
                .AnyAsync(r => r.EmployeeId == employeeId
                               && (r.Status == LeaveStatus.PENDING || r.Status == LeaveStatus.APPROVED)
                               && r.StartDate <= targetDate
                               && r.EndDate >= targetDate);
        }

        public async Task<(List<PermissionRequest> Items, int TotalCount)> GetPermissionRequestsAsync(
            int? employeeId,
            List<int>? allowedEmployeeIds,
            LeaveStatus? status,
            DateTime? date,
            int? year,
            int page,
            int pageSize)
        {
            var query = _dbContext.PermissionRequests.AsNoTracking();

            if (employeeId.HasValue)
            {
                query = query.Where(p => p.EmployeeId == employeeId.Value);
            }
            else if (allowedEmployeeIds != null)
            {
                query = query.Where(p => allowedEmployeeIds.Contains(p.EmployeeId));
            }

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (date.HasValue)
            {
                var targetDate = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
                query = query.Where(p => p.PermissionDate == targetDate);
            }
            else if (year.HasValue)
            {
                var yearStart = new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var yearEnd = new DateTime(year.Value, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                query = query.Where(p => p.PermissionDate >= yearStart && p.PermissionDate <= yearEnd);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
