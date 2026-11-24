using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Infrastructure.Repositories
{
    public class LeaveRepository : ILeaveRepository
    {
        private readonly WorkforceDbContext _dbContext;

        public LeaveRepository(WorkforceDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<LeaveType>> GetLeaveTypesAsync(bool activeOnly = true)
        {
            var query = _dbContext.LeaveTypes.AsNoTracking();
            if (activeOnly)
            {
                query = query.Where(t => t.IsActive);
            }
            return await query.OrderBy(t => t.Id).ToListAsync();
        }

        public async Task<LeaveType?> GetLeaveTypeByIdAsync(int id)
        {
            if (id <= 0) return null;
            return await _dbContext.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<LeaveBalance>> GetLeaveBalancesAsync(int employeeId, int year)
        {
            if (employeeId <= 0) return new List<LeaveBalance>();

            var existingBalances = await _dbContext.LeaveBalances
                .Include(b => b.LeaveType)
                .Where(b => b.EmployeeId == employeeId && b.Year == year)
                .ToListAsync();

            // Auto-initialize default balances for any active leave types if not already allocated for this year
            var allTypes = await _dbContext.LeaveTypes.Where(t => t.IsActive).ToListAsync();
            bool hasNewBalances = false;

            foreach (var type in allTypes)
            {
                if (!existingBalances.Any(b => b.LeaveTypeId == type.Id))
                {
                    var newBalance = new LeaveBalance(employeeId, type.Id, year, type.DefaultDaysPerYear, 0);
                    _dbContext.LeaveBalances.Add(newBalance);
                    existingBalances.Add(newBalance);
                    hasNewBalances = true;
                }
            }

            if (hasNewBalances)
            {
                await _dbContext.SaveChangesAsync();
                // Ensure navigation properties are populated
                foreach (var b in existingBalances)
                {
                    if (b.LeaveType == null!)
                    {
                        b.GetType().GetProperty(nameof(LeaveBalance.LeaveType))?
                            .SetValue(b, allTypes.FirstOrDefault(t => t.Id == b.LeaveTypeId));
                    }
                }
            }

            return existingBalances.OrderBy(b => b.LeaveTypeId).ToList();
        }

        public async Task<LeaveBalance?> GetLeaveBalanceAsync(int employeeId, int leaveTypeId, int year)
        {
            if (employeeId <= 0 || leaveTypeId <= 0) return null;

            var balance = await _dbContext.LeaveBalances
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year);

            if (balance == null)
            {
                var type = await _dbContext.LeaveTypes.FirstOrDefaultAsync(t => t.Id == leaveTypeId);
                if (type != null)
                {
                    balance = new LeaveBalance(employeeId, leaveTypeId, year, type.DefaultDaysPerYear, 0);
                    _dbContext.LeaveBalances.Add(balance);
                    await _dbContext.SaveChangesAsync();
                    balance.GetType().GetProperty(nameof(LeaveBalance.LeaveType))?.SetValue(balance, type);
                }
            }

            return balance;
        }

        public async Task AddLeaveBalanceAsync(LeaveBalance balance)
        {
            await _dbContext.LeaveBalances.AddAsync(balance);
        }

        public async Task AddLeaveRequestAsync(LeaveRequest request)
        {
            await _dbContext.LeaveRequests.AddAsync(request);
        }

        public async Task<LeaveRequest?> GetLeaveRequestByIdAsync(int id)
        {
            if (id <= 0) return null;
            return await _dbContext.LeaveRequests
                .Include(r => r.LeaveType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<bool> HasOverlappingLeaveAsync(int employeeId, DateTime startDate, DateTime endDate, int? excludeRequestId = null)
        {
            var start = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
            var end = DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc);

            var query = _dbContext.LeaveRequests
                .Where(r => r.EmployeeId == employeeId
                            && (r.Status == LeaveStatus.PENDING || r.Status == LeaveStatus.APPROVED)
                            && r.StartDate <= end
                            && r.EndDate >= start);

            if (excludeRequestId.HasValue)
            {
                query = query.Where(r => r.Id != excludeRequestId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<(List<LeaveRequest> Items, int TotalCount)> GetLeaveRequestsAsync(
            int? employeeId,
            List<int>? allowedEmployeeIds,
            LeaveStatus? status,
            int? year,
            int page,
            int pageSize)
        {
            var query = _dbContext.LeaveRequests
                .Include(r => r.LeaveType)
                .AsNoTracking();

            if (employeeId.HasValue)
            {
                query = query.Where(r => r.EmployeeId == employeeId.Value);
            }
            else if (allowedEmployeeIds != null)
            {
                query = query.Where(r => allowedEmployeeIds.Contains(r.EmployeeId));
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (year.HasValue)
            {
                var yearStart = new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var yearEnd = new DateTime(year.Value, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                query = query.Where(r => r.StartDate <= yearEnd && r.EndDate >= yearStart);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
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
