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
    public class SubmitPermissionRequestCommandHandler : IRequestHandler<SubmitPermissionRequestCommand, PermissionRequestDto>
    {
        private readonly IPermissionRepository _permissionRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public SubmitPermissionRequestCommandHandler(
            IPermissionRepository permissionRepository,
            IEmployeeRepository employeeRepository)
        {
            _permissionRepository = permissionRepository;
            _employeeRepository = employeeRepository;
        }

        public async Task<PermissionRequestDto> Handle(SubmitPermissionRequestCommand request, CancellationToken cancellationToken)
        {
            // 1. Resolve employee
            var employee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            if (employee == null)
            {
                throw new KeyNotFoundException("No employee profile found for the authenticated user.");
            }

            // 2. Active Employee Guard (BR-PERM-01)
            if (employee.Status != EmployeeStatus.ACTIVE)
            {
                throw new InvalidOperationException("Only active employees are permitted to submit permission requests.");
            }

            // 3. Parse and validate time format
            if (!TimeSpan.TryParse(request.StartTime, out var startTime))
            {
                throw new ArgumentException("Invalid StartTime format. Expected HH:mm or HH:mm:ss.");
            }

            if (!TimeSpan.TryParse(request.EndTime, out var endTime))
            {
                throw new ArgumentException("Invalid EndTime format. Expected HH:mm or HH:mm:ss.");
            }

            // 4. Chronological Guard (BR-PERM-02)
            if (endTime <= startTime)
            {
                throw new ArgumentException("EndTime must be later than StartTime.");
            }

            // 5. Duration Limits Guard (BR-PERM-01)
            decimal durationHours = Math.Round((decimal)(endTime - startTime).TotalHours, 2);
            if (durationHours > 4.0m)
            {
                throw new InvalidOperationException("Permission duration cannot exceed 4.0 hours. Please apply for half-day leave instead.");
            }

            if (durationHours < 0.5m)
            {
                throw new ArgumentException("Permission duration must be at least 30 minutes (0.5 hours).");
            }

            // 6. Overlap Guard with Leaves (BR-PERM-03)
            bool hasOverlappingLeave = await _permissionRepository.HasOverlappingLeaveOnDateAsync(employee.Id, request.PermissionDate);
            if (hasOverlappingLeave)
            {
                throw new InvalidOperationException("A leave request already exists covering this date.");
            }

            // 7. Overlap Guard with Other Permissions (BR-PERM-03)
            bool isOverlappingPermission = await _permissionRepository.HasOverlappingPermissionAsync(
                employee.Id,
                request.PermissionDate,
                startTime,
                endTime);

            if (isOverlappingPermission)
            {
                throw new InvalidOperationException("A permission request already exists for this time window with status PENDING or APPROVED.");
            }

            // 8. Instantiate and persist PermissionRequest
            var permission = new PermissionRequest(
                employee.Id,
                request.PermissionDate,
                startTime,
                endTime,
                durationHours,
                request.Reason);

            await _permissionRepository.AddPermissionRequestAsync(permission);
            await _permissionRepository.SaveChangesAsync();

            return new PermissionRequestDto
            {
                Id = permission.Id,
                EmployeeId = permission.EmployeeId,
                PermissionDate = permission.PermissionDate,
                StartTime = permission.StartTime.ToString(@"hh\:mm\:ss"),
                EndTime = permission.EndTime.ToString(@"hh\:mm\:ss"),
                DurationHours = permission.DurationHours,
                Reason = permission.Reason,
                Status = permission.Status.ToString(),
                ApproverId = permission.ApproverId,
                DecisionComments = permission.DecisionComments,
                DecidedAt = permission.DecidedAt,
                CreatedAt = permission.CreatedAt
            };
        }
    }
}
