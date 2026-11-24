using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class CheckInCommandHandler : IRequestHandler<CheckInCommand, AttendanceCheckInResponseDto>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IGeofenceValidationClient _geofenceClient;

        public CheckInCommandHandler(
            IEmployeeRepository employeeRepository,
            IAttendanceRepository attendanceRepository,
            IGeofenceValidationClient geofenceClient)
        {
            _employeeRepository = employeeRepository;
            _attendanceRepository = attendanceRepository;
            _geofenceClient = geofenceClient;
        }

        public async Task<AttendanceCheckInResponseDto> Handle(CheckInCommand request, CancellationToken cancellationToken)
        {
            // 1. Resolve employee by ActorUserId
            var employee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee record not found for User ID {request.ActorUserId}.");
            }

            // 2. BR-CHK-01: Employee Active Check
            if (employee.Status != EmployeeStatus.ACTIVE)
            {
                throw new InvalidOperationException("Deactivated or inactive employees cannot record attendance.");
            }

            // 3. BR-CHK-02: Active Check-In Session Guard (Allow multi-session per day once checked out)
            var now = DateTime.UtcNow;
            var existingAttendance = await _attendanceRepository.GetActiveAttendanceByEmployeeIdAsync(employee.Id, now.Date);
            if (existingAttendance != null)
            {
                throw new InvalidOperationException("An active check-in session is already in progress. Please check out first before starting a new check-in.");
            }

            // 4. BR-CHK-03: Geofence Boundary Validation
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
                    throw new InvalidOperationException($"Check-in rejected: Your current location is outside your assigned work geofence boundary. ({msg})".Trim());
                }
            }

            // 5. Create & Save Attendance Record
            var attendance = new AttendanceRecord(
                employee.Id,
                now,
                request.CheckInNotes,
                request.Latitude,
                request.Longitude);

            await _attendanceRepository.AddAsync(attendance);
            await _attendanceRepository.SaveChangesAsync();

            return new AttendanceCheckInResponseDto
            {
                AttendanceId = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                WorkDate = attendance.WorkDate.ToString("yyyy-MM-dd"),
                CheckInTime = attendance.CheckInTime,
                Status = attendance.Status.ToString(),
                CheckInNotes = attendance.CheckInNotes,
                Latitude = attendance.Latitude,
                Longitude = attendance.Longitude,
                Message = "Check-in recorded successfully."
            };
        }
    }
}
