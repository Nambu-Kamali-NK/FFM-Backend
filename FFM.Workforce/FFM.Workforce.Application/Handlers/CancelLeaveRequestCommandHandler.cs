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
    public class CancelLeaveRequestCommandHandler : IRequestHandler<CancelLeaveRequestCommand, LeaveRequestDto>
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public CancelLeaveRequestCommandHandler(
            ILeaveRepository leaveRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _leaveRepository = leaveRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<LeaveRequestDto> Handle(CancelLeaveRequestCommand request, CancellationToken cancellationToken)
        {
            var leaveRequest = await _leaveRepository.GetLeaveRequestByIdAsync(request.RequestId);
            if (leaveRequest == null)
            {
                throw new KeyNotFoundException($"Leave request with ID {request.RequestId} not found.");
            }

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));

            // Only the owner or an admin can cancel a leave request
            if (!isAdmin && (callerEmployee == null || callerEmployee.Id != leaveRequest.EmployeeId))
            {
                throw new UnauthorizedAccessException("You are only permitted to cancel your own leave requests.");
            }

            var previousStatus = leaveRequest.Status;

            // Domain cancellation method checks status and date constraints (BR-LEV-10)
            leaveRequest.Cancel();

            // If an APPROVED request is cancelled, refund the days back to the employee's balance (BR-LEV-10)
            if (previousStatus == LeaveStatus.APPROVED)
            {
                var balance = await _leaveRepository.GetLeaveBalanceAsync(
                    leaveRequest.EmployeeId,
                    leaveRequest.LeaveTypeId,
                    leaveRequest.StartDate.Year);

                if (balance != null)
                {
                    balance.RefundDays(leaveRequest.DaysCount);
                }
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
