using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class GetPermissionRequestsQueryHandler : IRequestHandler<GetPermissionRequestsQuery, PagedResult<PermissionRequestDto>>
    {
        private readonly IPermissionRepository _permissionRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public GetPermissionRequestsQueryHandler(
            IPermissionRepository permissionRepository,
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _permissionRepository = permissionRepository;
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<PagedResult<PermissionRequestDto>> Handle(GetPermissionRequestsQuery request, CancellationToken cancellationToken)
        {
            var roles = _currentUserService.GetRoles().ToList();
            bool isAdmin = roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                          r.Equals("SYSTEM ADMIN", StringComparison.OrdinalIgnoreCase));
            bool isManager = roles.Any(r => r.Equals("MANAGER", StringComparison.OrdinalIgnoreCase));

            var callerEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);

            int? targetEmployeeId = request.EmployeeId;
            List<int>? allowedEmployeeIds = null;

            if (!isAdmin)
            {
                if (callerEmployee == null)
                {
                    throw new KeyNotFoundException("No employee profile found for the authenticated user.");
                }

                if (!isManager)
                {
                    // Regular employee can ONLY view their own permission requests
                    targetEmployeeId = callerEmployee.Id;
                }
                else
                {
                    // Manager: can view own and direct reports / same org unit
                    if (targetEmployeeId.HasValue && targetEmployeeId.Value != callerEmployee.Id)
                    {
                        var targetEmp = await _employeeRepository.GetByIdAsync(targetEmployeeId.Value);
                        if (targetEmp == null || (targetEmp.OrgUnitId != callerEmployee.OrgUnitId && targetEmp.ManagerId != callerEmployee.Id))
                        {
                            throw new UnauthorizedAccessException("Managers can only view permission requests for their team members.");
                        }
                    }
                    else if (!targetEmployeeId.HasValue)
                    {
                        var allEmployees = await _employeeRepository.GetAllAsync();
                        allowedEmployeeIds = allEmployees
                            .Where(e => e.OrgUnitId == callerEmployee.OrgUnitId || e.ManagerId == callerEmployee.Id || e.Id == callerEmployee.Id)
                            .Select(e => e.Id)
                            .ToList();
                    }
                }
            }

            int page = Math.Max(1, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, 100);

            var (items, totalCount) = await _permissionRepository.GetPermissionRequestsAsync(
                targetEmployeeId,
                allowedEmployeeIds,
                request.Status,
                request.Date,
                request.Year,
                page,
                pageSize);

            var dtos = items.Select(p => new PermissionRequestDto
            {
                Id = p.Id,
                EmployeeId = p.EmployeeId,
                PermissionDate = p.PermissionDate,
                StartTime = p.StartTime.ToString(@"hh\:mm\:ss"),
                EndTime = p.EndTime.ToString(@"hh\:mm\:ss"),
                DurationHours = p.DurationHours,
                Reason = p.Reason,
                Status = p.Status.ToString(),
                ApproverId = p.ApproverId,
                DecisionComments = p.DecisionComments,
                DecidedAt = p.DecidedAt,
                CreatedAt = p.CreatedAt
            }).ToList();

            return new PagedResult<PermissionRequestDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }
    }
}
