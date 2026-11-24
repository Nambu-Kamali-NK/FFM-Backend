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
    public class CancelPermissionRequestCommandHandler : IRequestHandler<CancelPermissionRequestCommand, PermissionRequestDto>
    {
        private readonly IPermissionRepository _permissionRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public CancelPermissionRequestCommandHandler(
            IPermissionRepository permissionRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _permissionRepository = permissionRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<PermissionRequestDto> Handle(CancelPermissionRequestCommand request, CancellationToken cancellationToken)
        {
            var permission = await _permissionRepository.GetPermissionRequestByIdAsync(request.RequestId);
            if (permission == null)
            {
                throw new KeyNotFoundException($"Permission request with ID {request.RequestId} not found.");
            }

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));

            // Only the owner or an admin can cancel a permission request (BR-PERM-05)
            if (!isAdmin && (callerEmployee == null || callerEmployee.Id != permission.EmployeeId))
            {
                throw new UnauthorizedAccessException("You are only permitted to cancel your own permission requests.");
            }

            permission.Cancel();
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
