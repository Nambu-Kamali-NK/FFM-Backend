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
    public class DecidePermissionRequestCommandHandler : IRequestHandler<DecidePermissionRequestCommand, PermissionRequestDto>
    {
        private readonly IPermissionRepository _permissionRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public DecidePermissionRequestCommandHandler(
            IPermissionRepository permissionRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _permissionRepository = permissionRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<PermissionRequestDto> Handle(DecidePermissionRequestCommand request, CancellationToken cancellationToken)
        {
            // 1. RBAC Guard (BR-PERM-04): Only Managers and Admins
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));
            bool isManager = roles.Any(r => r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin && !isManager)
            {
                throw new UnauthorizedAccessException("Employees do not have permission to approve or reject permission requests.");
            }

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);

            // 2. Fetch target PermissionRequest
            var permission = await _permissionRepository.GetPermissionRequestByIdAsync(request.RequestId);
            if (permission == null)
            {
                throw new KeyNotFoundException($"Permission request with ID {request.RequestId} not found.");
            }

            if (permission.Status != LeaveStatus.PENDING)
            {
                throw new InvalidOperationException($"Cannot decide on permission request with status '{permission.Status}'. Only PENDING requests can be decided.");
            }

            // 3. Anti-Self-Approval Guard (BR-PERM-04)
            if (callerEmployee != null && callerEmployee.Id == permission.EmployeeId)
            {
                throw new InvalidOperationException("Self-approval or self-rejection of permission requests is strictly forbidden.");
            }

            // 4. OrgUnit / Direct Report Scope Check for Managers
            if (!isAdmin && callerEmployee != null)
            {
                var targetEmployee = await _employeeRepository.GetByIdAsync(permission.EmployeeId);
                if (targetEmployee != null && targetEmployee.OrgUnitId != callerEmployee.OrgUnitId && targetEmployee.ManagerId != callerEmployee.Id)
                {
                    throw new UnauthorizedAccessException("Managers can only approve permission requests for their team members.");
                }
            }

            // 5. Apply decision
            int approverId = callerEmployee?.Id ?? request.ActorUserId;

            if (request.Decision.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                permission.Approve(approverId, request.Comments);
            }
            else if (request.Decision.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))
            {
                permission.Reject(approverId, request.Comments);
            }
            else
            {
                throw new ArgumentException("Decision must be either 'APPROVED' or 'REJECTED'.");
            }

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
