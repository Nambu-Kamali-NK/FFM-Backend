using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class CorrectAttendanceCommandHandler : IRequestHandler<CorrectAttendanceCommand, CorrectAttendanceResponseDto>
    {
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly ICurrentUserService _currentUserService;

        public CorrectAttendanceCommandHandler(
            IAttendanceRepository attendanceRepository,
            ICurrentUserService currentUserService)
        {
            _attendanceRepository = attendanceRepository;
            _currentUserService = currentUserService;
        }

        public async Task<CorrectAttendanceResponseDto> Handle(CorrectAttendanceCommand request, CancellationToken cancellationToken)
        {
            // 1. RBAC Guard (BR-ATT-07): Reject field employees
            var roles = _currentUserService.GetRoles().ToList();
            bool isPrivileged = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                               r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                               r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                               r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            if (!isPrivileged)
            {
                throw new UnauthorizedAccessException("Insufficient permissions to correct attendance records.");
            }

            // 2. Fetch attendance record
            var attendance = await _attendanceRepository.GetByIdAsync(request.AttendanceId);
            if (attendance == null)
            {
                throw new KeyNotFoundException($"Attendance record with ID {request.AttendanceId} not found.");
            }

            // 3. Snapshot previous values for audit log
            var previousValues = JsonSerializer.Serialize(new
            {
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                Status = attendance.Status.ToString(),
                TotalWorkingHours = attendance.TotalWorkingHours
            });

            // 4. Perform correction domain logic
            attendance.Correct(request.CheckInTime, request.CheckOutTime, request.Reason);

            // 5. Snapshot new values for audit log
            var newValues = JsonSerializer.Serialize(new
            {
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                Status = attendance.Status.ToString(),
                TotalWorkingHours = attendance.TotalWorkingHours
            });

            // 6. Create immutable audit log (BR-ATT-10)
            int actorUserId = request.ActorUserId > 0 ? request.ActorUserId : _currentUserService.GetUserId();
            int effectiveAttendanceId = attendance.Id > 0 ? attendance.Id : request.AttendanceId;
            var auditLog = new AttendanceAuditLog(
                effectiveAttendanceId,
                actorUserId,
                "CORRECT_ATTENDANCE",
                previousValues,
                newValues,
                request.Reason);

            await _attendanceRepository.AddAuditLogAsync(auditLog);
            
            try
            {
                await _attendanceRepository.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                string detail = ex.InnerException?.Message ?? ex.Message;
                throw new InvalidOperationException($"Correction failed: {detail}", ex);
            }

            return new CorrectAttendanceResponseDto
            {
                AttendanceId = effectiveAttendanceId,
                CorrectedByUserId = actorUserId,
                Status = attendance.Status.ToString(),
                NetWorkingHours = attendance.TotalWorkingHours,
                Message = "Attendance record corrected successfully."
            };
        }
    }
}
