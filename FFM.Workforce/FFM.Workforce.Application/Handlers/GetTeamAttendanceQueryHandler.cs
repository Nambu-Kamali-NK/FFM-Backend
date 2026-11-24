using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class GetTeamAttendanceQueryHandler : IRequestHandler<GetTeamAttendanceQuery, PagedResult<TeamAttendanceDto>>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly ICurrentUserService _currentUserService;

        public GetTeamAttendanceQueryHandler(
            IEmployeeRepository employeeRepository,
            IAttendanceRepository attendanceRepository,
            ICurrentUserService currentUserService)
        {
            _employeeRepository = employeeRepository;
            _attendanceRepository = attendanceRepository;
            _currentUserService = currentUserService;
        }

        public async Task<PagedResult<TeamAttendanceDto>> Handle(GetTeamAttendanceQuery request, CancellationToken cancellationToken)
        {
            // 1. RBAC Guard (BR-TEM-03): Reject field employees
            var roles = _currentUserService.GetRoles().ToList();
            bool isPrivileged = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                               r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                               r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                               r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            if (!isPrivileged)
            {
                throw new UnauthorizedAccessException("Field employees do not have permission to view team attendance.");
            }

            // 2. Resolve requesting user's employee profile for OrgUnit filtering (BR-TEM-01)
            var managerEmp = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);

            Guid? targetOrgUnitId = request.OrgUnitId;
            int? managerEmployeeId = null;

            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin && managerEmp != null)
            {
                // Manager is scoped to their OrgUnitId or direct reports
                targetOrgUnitId = request.OrgUnitId ?? managerEmp.OrgUnitId;
                if (targetOrgUnitId != managerEmp.OrgUnitId)
                {
                    // Attempting to view outside their OrgUnit
                    managerEmployeeId = managerEmp.Id;
                    targetOrgUnitId = null;
                }
            }

            // 3. Query DB
            var (results, totalCount) = await _attendanceRepository.GetTeamAttendanceAsync(
                targetOrgUnitId,
                managerEmployeeId,
                request.Date,
                request.Status,
                request.Page,
                request.PageSize);

            // 4. Map DTOs
            var items = results.Select(x => new TeamAttendanceDto
            {
                Id = x.Attendance.Id,
                EmployeeId = x.Employee.Id,
                EmployeeCode = x.Employee.EmployeeCode,
                EmployeeName = x.Employee.Name,
                OrgUnitId = x.Employee.OrgUnitId,
                WorkDate = x.Attendance.WorkDate.ToString("yyyy-MM-dd"),
                CheckInTime = x.Attendance.CheckInTime,
                CheckOutTime = x.Attendance.CheckOutTime,
                Status = x.Attendance.Status.ToString(),
                TotalWorkingHours = x.Attendance.TotalWorkingHours,
                TotalBreakMinutes = x.Attendance.TotalBreakMinutes,
                Breaks = x.Attendance.Breaks.Select(b => new BreakDto
                {
                    Id = b.Id,
                    BreakType = b.BreakType,
                    BreakStartTime = b.BreakStartTime,
                    BreakEndTime = b.BreakEndTime,
                    DurationMinutes = b.DurationMinutes
                }).ToList()
            }).ToList();

            return new PagedResult<TeamAttendanceDto>(items, totalCount, request.Page, request.PageSize);
        }
    }
}
