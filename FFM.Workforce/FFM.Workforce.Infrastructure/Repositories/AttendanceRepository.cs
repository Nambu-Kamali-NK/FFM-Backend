using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Infrastructure.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly WorkforceDbContext _dbContext;

    public AttendanceRepository(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AttendanceRecord?> GetActiveAttendanceByEmployeeIdAsync(int employeeId, DateTime date)
    {
        if (employeeId <= 0) return null;

        return await _dbContext.AttendanceRecords
            .Include(a => a.Breaks)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.CheckOutTime == null);
    }

    public async Task<AttendanceRecord?> GetByIdAsync(int attendanceId)
    {
        if (attendanceId <= 0) return null;

        return await _dbContext.AttendanceRecords
            .FirstOrDefaultAsync(a => a.Id == attendanceId);
    }

    public async Task<(List<AttendanceRecord> Items, int TotalCount)> GetHistoryByEmployeeIdAsync(
        int employeeId,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize)
    {
        if (employeeId <= 0) return (new List<AttendanceRecord>(), 0);

        var query = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId);

        if (startDate.HasValue)
        {
            var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.WorkDate >= start);
        }

        if (endDate.HasValue)
        {
            var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(a => a.WorkDate <= end);
        }

        int totalCount = await query.CountAsync();

        int skip = (Math.Max(1, page) - 1) * pageSize;

        var items = await query
            .Include(a => a.Breaks)
            .OrderByDescending(a => a.WorkDate)
            .ThenByDescending(a => a.CheckInTime)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<(AttendanceRecord Attendance, Employee Employee)> Items, int TotalCount)> GetTeamAttendanceAsync(
        Guid? orgUnitId,
        int? managerEmployeeId,
        DateTime? date,
        AttendanceStatus? status,
        int page,
        int pageSize)
    {
        var targetDate = date.HasValue
            ? DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc)
            : DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var query = from a in _dbContext.AttendanceRecords.AsNoTracking().Include(x => x.Breaks)
                    join e in _dbContext.Employees.AsNoTracking() on a.EmployeeId equals e.Id
                    where a.WorkDate == targetDate
                    select new { Attendance = a, Employee = e };

        if (orgUnitId.HasValue)
        {
            query = query.Where(x => x.Employee.OrgUnitId == orgUnitId.Value);
        }
        else if (managerEmployeeId.HasValue)
        {
            query = query.Where(x => x.Employee.ManagerId == managerEmployeeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Attendance.Status == status.Value);
        }

        int totalCount = await query.CountAsync();

        int skip = (Math.Max(1, page) - 1) * pageSize;

        var items = await query
            .OrderBy(x => x.Employee.Name)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var resultItems = items.Select(x => (x.Attendance, x.Employee)).ToList();
        return (resultItems, totalCount);
    }

    public async Task AddAsync(AttendanceRecord attendance)
    {
        await _dbContext.AttendanceRecords.AddAsync(attendance);
    }

    public async Task AddAuditLogAsync(AttendanceAuditLog auditLog)
    {
        await _dbContext.AttendanceAuditLogs.AddAsync(auditLog);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
