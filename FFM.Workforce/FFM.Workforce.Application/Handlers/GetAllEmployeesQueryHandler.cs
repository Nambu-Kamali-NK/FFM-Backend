using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class GetAllEmployeesQueryHandler : IRequestHandler<GetAllEmployeesQuery, PagedResult<EmployeeDto>>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public GetAllEmployeesQueryHandler(
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<PagedResult<EmployeeDto>> Handle(
            GetAllEmployeesQuery request,
            CancellationToken cancellationToken)
        {
            var employees = await _employeeRepository.GetAllAsync();
            var roles = _currentUserService.GetRoles().ToList();
            var currentUserId = _currentUserService.GetUserId();

            IEnumerable<Employee> scopedEmployees;

            // 1. Role-based Security Scope Filtering
            if (roles.Contains("SYSTEM_ADMIN") || roles.Contains("ADMIN") || roles.Contains("EXECUTIVE"))
            {
                scopedEmployees = employees;
            }
            else
            {
                var callerEmployee = await _employeeRepository.GetByUserIdAsync(currentUserId);
                if (callerEmployee != null)
                {
                    if (roles.Contains("MANAGER"))
                    {
                        scopedEmployees = employees.Where(e =>
                            e.UserId == currentUserId ||
                            e.OrgUnitId == callerEmployee.OrgUnitId ||
                            (e.ManagerId.HasValue && e.ManagerId.Value == callerEmployee.Id));
                    }
                    else if (roles.Contains("SUPERVISOR"))
                    {
                        scopedEmployees = employees.Where(e =>
                            e.UserId == currentUserId ||
                            (e.ManagerId.HasValue && e.ManagerId.Value == callerEmployee.Id));
                    }
                    else
                    {
                        scopedEmployees = employees.Where(e => e.UserId == currentUserId);
                    }
                }
                else
                {
                    scopedEmployees = employees.Where(e => e.UserId == currentUserId);
                }
            }

            // 2. Dynamic Sorting (SortBy & SortOrder "asc" / "desc")
            var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "employeecode" : request.SortBy.Trim().ToLowerInvariant();
            bool isDesc = !string.IsNullOrWhiteSpace(request.SortOrder) && request.SortOrder.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

            scopedEmployees = sortBy switch
            {
                "employeecode" or "employee_code" => isDesc ? scopedEmployees.OrderByDescending(e => e.EmployeeCode) : scopedEmployees.OrderBy(e => e.EmployeeCode),
                "name" => isDesc ? scopedEmployees.OrderByDescending(e => e.Name) : scopedEmployees.OrderBy(e => e.Name),
                "status" => isDesc ? scopedEmployees.OrderByDescending(e => GetStatusPriority(e.Status)) : scopedEmployees.OrderBy(e => GetStatusPriority(e.Status)),
                _ => isDesc ? scopedEmployees.OrderByDescending(e => e.EmployeeCode) : scopedEmployees.OrderBy(e => e.EmployeeCode)
            };

            int totalCount = scopedEmployees is ICollection<Employee> col ? col.Count : scopedEmployees.Count();

            // 3. Pagination (PageNumber & PageSize)
            int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            int pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

            var pagedItems = scopedEmployees
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            return new PagedResult<EmployeeDto>(pagedItems, totalCount, pageNumber, pageSize);
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

        private static int GetStatusPriority(EmployeeStatus status) => status switch
        {
            EmployeeStatus.ACTIVE => 1,
            EmployeeStatus.PENDING_ACTIVATION => 2,
            EmployeeStatus.DEACTIVATED => 3,
            _ => 4
        };
    }
}
