using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using MediatR;

namespace FFM.Workforce.Application.Handlers
{
    public class BatchDeactivateEmployeesCommandHandler : IRequestHandler<BatchDeactivateEmployeesCommand, int>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public BatchDeactivateEmployeesCommandHandler(
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<int> Handle(BatchDeactivateEmployeesCommand request, CancellationToken cancellationToken)
        {
            var actorRoles = _currentUserService.GetRoles();
            if (!actorRoles.Contains("SYSTEM_ADMIN") &&
                !actorRoles.Contains("ADMIN") &&
                !actorRoles.Contains("MANAGER"))
            {
                throw new UnauthorizedAccessException("Actor does not have permission to deactivate employees.");
            }

            if (request.EmployeeIds == null || !request.EmployeeIds.Any())
            {
                throw new InvalidOperationException("At least one employee ID must be provided for batch deactivation.");
            }

            int count = 0;
            foreach (var id in request.EmployeeIds.Distinct())
            {
                var employee = await _employeeRepository.GetByIdAsync(id);
                if (employee != null && employee.Status != EmployeeStatus.DEACTIVATED)
                {
                    employee.Deactivate();
                    count++;
                }
            }

            if (count > 0)
            {
                await _employeeRepository.SaveChangesAsync();
            }

            return count;
        }
    }
}
