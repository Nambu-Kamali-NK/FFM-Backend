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
    public class DeactivateEmployeeCommandHandler : IRequestHandler<DeactivateEmployeeCommand, bool>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ICurrentUserService _currentUserService;

        public DeactivateEmployeeCommandHandler(
            IEmployeeRepository employeeRepository,
            ICurrentUserService currentUserService)
        {
            _employeeRepository = employeeRepository;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeactivateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var actorRoles = _currentUserService.GetRoles();
            if (!actorRoles.Contains("SYSTEM_ADMIN") &&
                !actorRoles.Contains("ADMIN") &&
                !actorRoles.Contains("MANAGER"))
            {
                throw new UnauthorizedAccessException("Actor does not have permission to deactivate employees.");
            }

            var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {request.EmployeeId} not found.");
            }

            employee.Deactivate();
            await _employeeRepository.SaveChangesAsync();
            return true;
        }
    }
}
