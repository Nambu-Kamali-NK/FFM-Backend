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
    public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, AttendanceCheckOutResponseDto>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IGeofenceValidationClient _geofenceClient;

        public CheckOutCommandHandler(
            IEmployeeRepository employeeRepository,
            IAttendanceRepository attendanceRepository,
            IGeofenceValidationClient geofenceClient)
        {
            _employeeRepository = employeeRepository;
            _attendanceRepository = attendanceRepository;
            _geofenceClient = geofenceClient;
        }

        public async Task<AttendanceCheckOutResponseDto> Handle(CheckOutCommand request, CancellationToken cancellationToken)
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

            // 3. BR-ATT-03: Retrieve active check-in record
            var now = DateTime.UtcNow;
            var attendance = await _attendanceRepository.GetActiveAttendanceByEmployeeIdAsync(employee.Id, now.Date);
            if (attendance == null)
            {
                throw new InvalidOperationException("No active check-in record found for checkout.");
            }

            // 4. BR-COUT-02: Geofence Boundary Validation
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                GeofenceValidationResult? geofenceResult;
                try
                {
                    geofenceResult = await _geofenceClient.ValidateLocationAsync(
                        employee.Id,
                        request.Latitude.Value,
                        request.Longitude.Value);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Geofence location verification service is temporarily unavailable. Please retry in a few moments.", ex);
                }

                if (geofenceResult == null || !geofenceResult.IsValidGeofence)
                {
                    var msg = geofenceResult?.Message ?? "Unable to verify geofence boundary.";
                    throw new InvalidOperationException($"Check-out rejected: Your current location is outside your assigned work geofence boundary. ({msg})".Trim());
                }
            }

            // 5. Perform domain checkout calculation
            attendance.CheckOut(now, request.CheckOutNotes);

            // 6. Persist changes
            await _attendanceRepository.SaveChangesAsync();

            return new AttendanceCheckOutResponseDto
            {
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                WorkDate = attendance.WorkDate.ToString("yyyy-MM-dd"),
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime!.Value,
                TotalBreakMinutes = attendance.TotalBreakMinutes,
                NetWorkingHours = attendance.TotalWorkingHours,
                Status = attendance.Status.ToString(),
                CheckOutNotes = attendance.CheckOutNotes,
                Message = "Check-out recorded successfully."
            };
        }
    }
}
