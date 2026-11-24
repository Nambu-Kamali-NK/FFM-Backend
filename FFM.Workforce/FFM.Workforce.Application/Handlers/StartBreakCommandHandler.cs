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
    public class StartBreakCommandHandler : IRequestHandler<StartBreakCommand, StartBreakResponseDto>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;

        public StartBreakCommandHandler(
            IEmployeeRepository employeeRepository,
            IAttendanceRepository attendanceRepository)
        {
            _employeeRepository = employeeRepository;
            _attendanceRepository = attendanceRepository;
        }

        public async Task<StartBreakResponseDto> Handle(StartBreakCommand request, CancellationToken cancellationToken)
        {
            // 1. Resolve employee by ActorUserId
            var employee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee record not found for User ID {request.ActorUserId}.");
            }

            // 2. Employee Active Check (BR-BRK-01)
            if (employee.Status != EmployeeStatus.ACTIVE)
            {
                throw new InvalidOperationException("Deactivated or inactive employees cannot record attendance.");
            }

            // 3. BR-BRK-02: Retrieve active check-in record for current UTC date
            var now = DateTime.UtcNow;
            var attendance = await _attendanceRepository.GetActiveAttendanceByEmployeeIdAsync(employee.Id, now.Date);
            if (attendance == null)
            {
                throw new InvalidOperationException("Employee must be checked in to start a break.");
            }

            // 4. Perform domain start break logic (BR-BRK-03 & status transition PRESENT -> ON_BREAK)
            var breakRecord = attendance.StartBreak(request.BreakType, now);

            // 5. Persist changes
            await _attendanceRepository.SaveChangesAsync();

            return new StartBreakResponseDto
            {
                BreakId = breakRecord.Id,
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                BreakType = breakRecord.BreakType,
                BreakStartTime = breakRecord.BreakStartTime,
                Status = attendance.Status.ToString(),
                Message = "Break started successfully."
            };
        }
    }
}
