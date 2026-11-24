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
    public class GetLeaveBalancesQueryHandler : IRequestHandler<GetLeaveBalancesQuery, List<LeaveBalanceDto>>
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public GetLeaveBalancesQueryHandler(
            ILeaveRepository leaveRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _leaveRepository = leaveRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<List<LeaveBalanceDto>> Handle(GetLeaveBalancesQuery request, CancellationToken cancellationToken)
        {
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));
            bool isManager = roles.Any(r => r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);

            int targetEmployeeId;
            if (!request.TargetEmployeeId.HasValue || (callerEmployee != null && request.TargetEmployeeId.Value == callerEmployee.Id))
            {
                if (callerEmployee == null)
                {
                    throw new KeyNotFoundException("No employee profile found for the authenticated user.");
                }
                targetEmployeeId = callerEmployee.Id;
            }
            else
            {
                // Requesting another employee's balance
                targetEmployeeId = request.TargetEmployeeId.Value;

                if (!isAdmin && !isManager)
                {
                    throw new UnauthorizedAccessException("Employees are not authorized to view other employees' leave balances.");
                }

                var targetEmp = await _employeeRepository.GetByIdAsync(targetEmployeeId);
                if (targetEmp == null)
                {
                    throw new KeyNotFoundException($"Employee with ID {targetEmployeeId} not found.");
                }

                if (!isAdmin && isManager && callerEmployee != null)
                {
                    // Manager check: must be in same org unit or direct report
                    if (targetEmp.OrgUnitId != callerEmployee.OrgUnitId && targetEmp.ManagerId != callerEmployee.Id)
                    {
                        throw new UnauthorizedAccessException("Managers can only view leave balances for their team members.");
                    }
                }
            }

            int year = request.Year ?? DateTime.UtcNow.Year;
            var balances = await _leaveRepository.GetLeaveBalancesAsync(targetEmployeeId, year);

            return balances.Select(b => new LeaveBalanceDto
            {
                LeaveTypeId = b.LeaveTypeId,
                LeaveTypeCode = b.LeaveType?.Code ?? string.Empty,
                LeaveTypeName = b.LeaveType?.Name ?? string.Empty,
                Year = b.Year,
                TotalDays = b.TotalDays,
                UsedDays = b.UsedDays,
                AvailableDays = b.AvailableDays
            }).ToList();
        }
    }
}
