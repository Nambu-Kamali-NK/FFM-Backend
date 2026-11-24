using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class GetLeaveRequestsQueryHandler : IRequestHandler<GetLeaveRequestsQuery, PagedResult<LeaveRequestDto>>
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public GetLeaveRequestsQueryHandler(
            ILeaveRepository leaveRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _leaveRepository = leaveRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<PagedResult<LeaveRequestDto>> Handle(GetLeaveRequestsQuery request, CancellationToken cancellationToken)
        {
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));
            bool isManager = roles.Any(r => r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);

            int? targetEmployeeId = request.EmployeeId;
            List<int>? allowedEmployeeIds = null;

            if (!isAdmin)
            {
                if (callerEmployee == null)
                {
                    throw new KeyNotFoundException("No employee profile found for the authenticated user.");
                }

                if (!isManager)
                {
                    // Regular employee can ONLY see their own requests
                    targetEmployeeId = callerEmployee.Id;
                }
                else
                {
                    // Manager: if requesting a specific employee, ensure they belong to the same team or direct report
                    if (targetEmployeeId.HasValue && targetEmployeeId.Value != callerEmployee.Id)
                    {
                        var targetEmp = await _employeeRepository.GetByIdAsync(targetEmployeeId.Value);
                        if (targetEmp == null || (targetEmp.OrgUnitId != callerEmployee.OrgUnitId && targetEmp.ManagerId != callerEmployee.Id))
                        {
                            throw new UnauthorizedAccessException("Managers can only view leave requests for their team members.");
                        }
                    }
                    else if (!targetEmployeeId.HasValue)
                    {
                        // Get all employees in manager's org unit or direct reports
                        var allEmployees = await _employeeRepository.GetAllAsync();
                        allowedEmployeeIds = allEmployees
                            .Where(e => e.OrgUnitId == callerEmployee.OrgUnitId || e.ManagerId == callerEmployee.Id || e.Id == callerEmployee.Id)
                            .Select(e => e.Id)
                            .ToList();
                    }
                }
            }

            int page = Math.Max(1, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 100);

            var (items, totalCount) = await _leaveRepository.GetLeaveRequestsAsync(
                targetEmployeeId,
                allowedEmployeeIds,
                request.Status,
                request.Year,
                page,
                pageSize);

            var dtos = items.Select(r => new LeaveRequestDto
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                LeaveTypeId = r.LeaveTypeId,
                LeaveTypeName = r.LeaveType?.Name ?? string.Empty,
                LeaveTypeCode = r.LeaveType?.Code ?? string.Empty,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                DaysCount = r.DaysCount,
                IsHalfDay = r.IsHalfDay,
                Reason = r.Reason,
                Status = r.Status.ToString(),
                ApproverId = r.ApproverId,
                DecisionComments = r.DecisionComments,
                DecidedAt = r.DecidedAt,
                CreatedAt = r.CreatedAt
            }).ToList();

            return new PagedResult<LeaveRequestDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }
    }
}
