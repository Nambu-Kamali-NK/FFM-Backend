using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Application.Interfaces
{
    public interface ILeaveRepository
    {
        Task<List<LeaveType>> GetLeaveTypesAsync(bool activeOnly = true);
        Task<LeaveType?> GetLeaveTypeByIdAsync(int id);
        Task<List<LeaveBalance>> GetLeaveBalancesAsync(int employeeId, int year);
        Task<LeaveBalance?> GetLeaveBalanceAsync(int employeeId, int leaveTypeId, int year);
        Task AddLeaveBalanceAsync(LeaveBalance balance);
        Task AddLeaveRequestAsync(LeaveRequest request);
        Task<LeaveRequest?> GetLeaveRequestByIdAsync(int id);
        Task<bool> HasOverlappingLeaveAsync(int employeeId, DateTime startDate, DateTime endDate, int? excludeRequestId = null);
        Task<(List<LeaveRequest> Items, int TotalCount)> GetLeaveRequestsAsync(
            int? employeeId,
            List<int>? allowedEmployeeIds,
            LeaveStatus? status,
            int? year,
            int page,
            int pageSize);
        Task SaveChangesAsync();
    }
}
