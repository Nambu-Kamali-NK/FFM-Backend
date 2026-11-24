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
    public class EndBreakCommandHandler : IRequestHandler<EndBreakCommand, EndBreakResponseDto>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;

        public EndBreakCommandHandler(
            IEmployeeRepository employeeRepository,
            IAttendanceRepository attendanceRepository)
        {
            _employeeRepository = employeeRepository;
            _attendanceRepository = attendanceRepository;
        }

        public async Task<EndBreakResponseDto> Handle(EndBreakCommand request, CancellationToken cancellationToken)
        {
            // 1. Resolve employee by ActorUserId
            var employee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee record not found for User ID {request.ActorUserId}.");
            }

            // 2. Employee Active Check
            if (employee.Status != EmployeeStatus.ACTIVE)
            {
                throw new InvalidOperationException("Deactivated or inactive employees cannot record attendance.");
            }

            // 3. Retrieve active check-in record for current UTC date
            var now = DateTime.UtcNow;
            var attendance = await _attendanceRepository.GetActiveAttendanceByEmployeeIdAsync(employee.Id, now.Date);
            if (attendance == null)
            {
                throw new InvalidOperationException("No active check-in record found to end break.");
            }

            // 4. Perform domain end break logic (BR-BRK-05, BR-BRK-06, BR-BRK-07, BR-BRK-08)
            var endedBreak = attendance.EndBreak(now);

            // 5. Persist changes
            await _attendanceRepository.SaveChangesAsync();

            return new EndBreakResponseDto
            {
                BreakId = endedBreak.Id,
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                BreakType = endedBreak.BreakType,
                BreakStartTime = endedBreak.BreakStartTime,
                BreakEndTime = endedBreak.BreakEndTime!.Value,
                DurationMinutes = endedBreak.DurationMinutes ?? 0,
                TotalBreakMinutes = attendance.TotalBreakMinutes,
                Status = attendance.Status.ToString(),
                Message = "Break ended successfully."
            };
        }
    }
}
