using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Application.Interfaces
{
    public interface IPermissionRepository
    {
        Task AddPermissionRequestAsync(PermissionRequest request);
        Task<PermissionRequest?> GetPermissionRequestByIdAsync(int id);
        Task<bool> HasOverlappingPermissionAsync(int employeeId, DateTime date, TimeSpan startTime, TimeSpan endTime, int? excludeRequestId = null);
        Task<bool> HasOverlappingLeaveOnDateAsync(int employeeId, DateTime date);
        Task<(List<PermissionRequest> Items, int TotalCount)> GetPermissionRequestsAsync(
            int? employeeId,
            List<int>? allowedEmployeeIds,
            LeaveStatus? status,
            DateTime? date,
            int? year,
            int page,
            int pageSize);
        Task SaveChangesAsync();
    }
}
