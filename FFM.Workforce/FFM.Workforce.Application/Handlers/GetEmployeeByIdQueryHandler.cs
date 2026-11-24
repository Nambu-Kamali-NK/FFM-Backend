using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers;

public class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto?>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeeByIdQueryHandler(
        IEmployeeRepository employeeRepository,
        ICurrentUserService currentUserService)
    {
        _employeeRepository = employeeRepository;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeDto?> Handle(
        GetEmployeeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);

        if (employee == null)
            return null;

        var roles = _currentUserService.GetRoles().ToList();
        var currentUserId = _currentUserService.GetUserId();

        // 1. Global administrative and executive roles have organization-wide access
        if (roles.Contains("SYSTEM_ADMIN") || roles.Contains("ADMIN") || roles.Contains("EXECUTIVE"))
        {
            return MapToDto(employee);
        }

        // 2. Caller accessing their own employee record
        if (employee.UserId == currentUserId)
        {
            return MapToDto(employee);
        }

        // 3. For MANAGER or SUPERVISOR roles, verify reporting hierarchy or organization boundary
        if (roles.Contains("MANAGER") || roles.Contains("SUPERVISOR"))
        {
            var callerEmployee = await _employeeRepository.GetByUserIdAsync(currentUserId);
            if (callerEmployee != null)
            {
                // Direct report check (valid for both Manager and Supervisor)
                if (employee.ManagerId.HasValue && employee.ManagerId.Value == callerEmployee.Id)
                {
                    return MapToDto(employee);
                }

                // Managers can view employees in their assigned organizational unit
                if (roles.Contains("MANAGER") && employee.OrgUnitId == callerEmployee.OrgUnitId)
                {
                    return MapToDto(employee);
                }
            }
        }

        // 4. Access denied if no rule matched
        throw new UnauthorizedAccessException("You do not have permission to view this employee record.");
    }

    private static EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            UserId = employee.UserId,
            Name = employee.Name,
            RoleId = employee.RoleId,
            OrgUnitId = employee.OrgUnitId,
            ManagerId = employee.ManagerId,
            Email = employee.Email,
            Mobile = employee.Mobile,
            Status = employee.Status.ToString(),
            CreatedAt = employee.CreatedAt,
            CreatedBy = string.IsNullOrWhiteSpace(employee.CreatedBy) ? "System Admin" : employee.CreatedBy
        };
    }
}

