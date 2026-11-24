using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Application.Interfaces
{
    public interface IAttendanceRepository
    {
        Task<AttendanceRecord?> GetActiveAttendanceByEmployeeIdAsync(int employeeId, DateTime date);
        Task<AttendanceRecord?> GetByIdAsync(int attendanceId);
        Task<(List<AttendanceRecord> Items, int TotalCount)> GetHistoryByEmployeeIdAsync(
            int employeeId,
            DateTime? startDate,
            DateTime? endDate,
            int page,
            int pageSize);
        Task<(List<(AttendanceRecord Attendance, Employee Employee)> Items, int TotalCount)> GetTeamAttendanceAsync(
            Guid? orgUnitId,
            int? managerEmployeeId,
            DateTime? date,
            AttendanceStatus? status,
            int page,
            int pageSize);
        Task AddAsync(AttendanceRecord attendance);
        Task AddAuditLogAsync(AttendanceAuditLog auditLog);
        Task SaveChangesAsync();
    }
}
