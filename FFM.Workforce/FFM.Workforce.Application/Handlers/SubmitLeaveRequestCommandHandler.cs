using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class SubmitLeaveRequestCommandHandler : IRequestHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public SubmitLeaveRequestCommandHandler(
            ILeaveRepository leaveRepository,
            IEmployeeRepository employeeRepository)
        {
            _leaveRepository = leaveRepository;
            _employeeRepository = employeeRepository;
        }

        public async Task<LeaveRequestDto> Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
        {
            // 1. Resolve employee
            var employee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (employee == null)
            {
                throw new KeyNotFoundException("No employee profile found for the authenticated user.");
            }

            // 2. Active Employee Guard (BR-LEV-01)
            if (employee.Status != EmployeeStatus.ACTIVE)
            {
                throw new InvalidOperationException("Only active employees are permitted to submit leave requests.");
            }

            // 3. Chronological check (BR-LEV-04)
            if (request.EndDate.Date < request.StartDate.Date)
            {
                throw new ArgumentException("EndDate cannot be earlier than StartDate.");
            }

            // 4. Overlap Guard (BR-LEV-03)
            bool isOverlapping = await _leaveRepository.HasOverlappingLeaveAsync(employee.Id, request.StartDate, request.EndDate);
            if (isOverlapping)
            {
                throw new InvalidOperationException("A leave request already exists for this date range with status PENDING or APPROVED.");
            }

            // 5. Calculate Days Count (BR-LEV-05)
            decimal daysCount;
            if (request.IsHalfDay)
            {
                if (request.StartDate.Date != request.EndDate.Date)
                {
                    throw new ArgumentException("Half-day leave requests must have identical StartDate and EndDate.");
                }
                daysCount = 0.5m;
            }
            else
            {
                daysCount = (decimal)((request.EndDate.Date - request.StartDate.Date).Days + 1);
            }

            // 6. Balance Sufficiency Guard (BR-LEV-02)
            var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(request.LeaveTypeId);
            if (leaveType == null || !leaveType.IsActive)
            {
                throw new KeyNotFoundException($"Active leave type with ID {request.LeaveTypeId} not found.");
            }

            int year = request.StartDate.Year;
            var balance = await _leaveRepository.GetLeaveBalanceAsync(employee.Id, leaveType.Id, year);
            if (balance == null || !balance.CanDeduct(daysCount))
            {
                decimal available = balance?.AvailableDays ?? 0.0m;
                throw new InvalidOperationException($"Requested {daysCount:F1} days exceeds available balance of {available:F1} day(s) for {leaveType.Name}.");
            }

            // 7. Instantiate and persist LeaveRequest
            var leaveRequest = new LeaveRequest(
                employee.Id,
                leaveType.Id,
                request.StartDate,
                request.EndDate,
                daysCount,
                request.IsHalfDay,
                request.Reason);

            await _leaveRepository.AddLeaveRequestAsync(leaveRequest);
            await _leaveRepository.SaveChangesAsync();

            return new LeaveRequestDto
            {
                Id = leaveRequest.Id,
                EmployeeId = leaveRequest.EmployeeId,
                LeaveTypeId = leaveRequest.LeaveTypeId,
                LeaveTypeName = leaveType.Name,
                LeaveTypeCode = leaveType.Code,
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
