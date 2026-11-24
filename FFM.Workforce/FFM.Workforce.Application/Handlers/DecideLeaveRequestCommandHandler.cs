using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class DecideLeaveRequestCommandHandler : IRequestHandler<DecideLeaveRequestCommand, LeaveRequestDto>
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public DecideLeaveRequestCommandHandler(
            ILeaveRepository leaveRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _leaveRepository = leaveRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<LeaveRequestDto> Handle(DecideLeaveRequestCommand request, CancellationToken cancellationToken)
        {
            // 1. RBAC Guard (BR-LEV-08): Only Managers and Admins
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));
            bool isManager = roles.Any(r => r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin && !isManager)
            {
                throw new UnauthorizedAccessException("Employees do not have permission to approve or reject leave requests.");
            }

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);

            // 2. Fetch the target LeaveRequest
            var leaveRequest = await _leaveRepository.GetLeaveRequestByIdAsync(request.RequestId);
            if (leaveRequest == null)
            {
                throw new KeyNotFoundException($"Leave request with ID {request.RequestId} not found.");
            }

            if (leaveRequest.Status != LeaveStatus.PENDING)
            {
                throw new InvalidOperationException($"Cannot decide on leave request with status '{leaveRequest.Status}'. Only PENDING requests can be decided.");
            }

            // 3. Anti-Self-Approval Guard (BR-LEV-08)
            if (callerEmployee != null && callerEmployee.Id == leaveRequest.EmployeeId)
            {
                throw new InvalidOperationException("Self-approval is strictly forbidden. A manager cannot approve or reject their own leave request.");
            }

            // 4. Manager Scope Guard
            if (!isAdmin && isManager && callerEmployee != null)
            {
                var targetEmployee = await _employeeRepository.GetByIdAsync(leaveRequest.EmployeeId);
                if (targetEmployee != null && targetEmployee.OrgUnitId != callerEmployee.OrgUnitId && targetEmployee.ManagerId != callerEmployee.Id)
                {
                    throw new UnauthorizedAccessException("Managers can only approve or reject leave requests for their team members.");
                }
            }

            int approverEmpId = callerEmployee?.Id ?? 1;

            // 5. Process Decision
            if (request.Decision.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                // Atomic Balance Deduction (BR-LEV-09)
                var balance = await _leaveRepository.GetLeaveBalanceAsync(
                    leaveRequest.EmployeeId,
                    leaveRequest.LeaveTypeId,
                    leaveRequest.StartDate.Year);

                if (balance == null || !balance.CanDeduct(leaveRequest.DaysCount))
                {
                    decimal available = balance?.AvailableDays ?? 0.0m;
                    throw new InvalidOperationException($"Cannot approve leave request: Requested {leaveRequest.DaysCount:F1} day(s) exceeds available balance of {available:F1} day(s).");
                }

                balance.DeductDays(leaveRequest.DaysCount);
                leaveRequest.Approve(approverEmpId, request.Comments);
            }
            else if (request.Decision.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))
            {
                leaveRequest.Reject(approverEmpId, request.Comments);
            }
            else
            {
                throw new ArgumentException("Decision must be either 'APPROVED' or 'REJECTED'.");
            }

            await _leaveRepository.SaveChangesAsync();

            return new LeaveRequestDto
            {
                Id = leaveRequest.Id,
                EmployeeId = leaveRequest.EmployeeId,
                LeaveTypeId = leaveRequest.LeaveTypeId,
                LeaveTypeName = leaveRequest.LeaveType?.Name ?? string.Empty,
                LeaveTypeCode = leaveRequest.LeaveType?.Code ?? string.Empty,
                StartDate = leaveRequest.StartDate,
                EndDate = leaveRequest.EndDate,
                DaysCount = leaveRequest.DaysCount,
                IsHalfDay = leaveRequest.IsHalfDay,
                Reason = leaveRequest.Reason,
                Status = leaveRequest.Status.ToString(),
                ApproverId = leaveRequest.ApproverId,
                DecisionComments = leaveRequest.DecisionComments,
                DecidedAt = leaveRequest.DecidedAt,
                CreatedAt = leaveRequest.CreatedAt
            };
        }
    }
}
