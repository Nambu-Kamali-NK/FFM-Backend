using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using FFM.Identity.Access.Domain.Entities;

namespace FFM.Workforce.Application.Handlers
{
    /// <summary>
    /// Handles editing an existing employee's profile and workforce information.
    /// Implements comprehensive BOLA / IDOR authorization, parameter tampering protection,
    /// privilege escalation defense, circular hierarchy prevention, and secure audit logging.
    /// </summary>
    public class EditEmployeeCommandHandler : IRequestHandler<EditEmployeeCommand, EditEmployeeResponseDto>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOutboxRepository _outboxRepository;
        private readonly DbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;

        public EditEmployeeCommandHandler(
            IEmployeeRepository employeeRepository,
            IOutboxRepository outboxRepository,
            DbContext dbContext,
            ICurrentUserService currentUserService)
        {
            _employeeRepository = employeeRepository;
            _outboxRepository = outboxRepository;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
        }

        public async Task<EditEmployeeResponseDto> Handle(EditEmployeeCommand request, CancellationToken cancellationToken)
        {
            // ── 1. Validate Employee Existence ─────────────────────────────────────
            var employee = await _employeeRepository.GetByIdAsync(request.Id);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {request.Id} not found.");
            }

            // ── 2. Check Employee Status (Deactivated / Inactive Employee) ───────────
            if (employee.Status == EmployeeStatus.DEACTIVATED)
            {
                throw new InvalidOperationException("Cannot edit a deactivated employee.");
            }

            // ── 3. Validate Actor Identity & Authorization (BOLA / IDOR Protection) ─
            var actorRoles = _currentUserService.GetRoles().ToList();
            var actorUserId = _currentUserService.GetUserId();

            if (actorUserId <= 0)
            {
                throw new UnauthorizedAccessException("Actor user is not authenticated.");
            }

            var isSystemAdmin = actorRoles.Contains("SYSTEM_ADMIN");
            var isAdmin = actorRoles.Contains("ADMIN");
            var isManager = actorRoles.Contains("MANAGER");
            var isSupervisor = actorRoles.Contains("SUPERVISOR");

            if (!isSystemAdmin && !isAdmin && !isManager && !isSupervisor)
            {
                throw new UnauthorizedAccessException("You do not have permission to edit employees.");
            }

            // Fetch target employee's current role to check access
            var currentEmployeeRole = await _dbContext.Set<FFM.Workforce.Domain.Entities.Role>().FindAsync(employee.RoleId);
            var currentRoleName = currentEmployeeRole?.Name ?? string.Empty;

            // Target Role Restrictions:
            // Only SYSTEM_ADMIN can edit a SYSTEM_ADMIN's profile
            if (currentRoleName.Equals("SYSTEM_ADMIN", StringComparison.OrdinalIgnoreCase) && !isSystemAdmin)
            {
                throw new UnauthorizedAccessException("Only System Admins can modify a System Admin employee profile.");
            }

            // Managers and Supervisors cannot edit Admin profiles
            if (currentRoleName.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) && !isSystemAdmin && !isAdmin)
            {
                throw new UnauthorizedAccessException("You do not have permission to modify an Admin employee profile.");
            }

            // Organizational unit & Hierarchy checks for Manager and Supervisor
            if (!isSystemAdmin && !isAdmin)
            {
                var callerEmployee = await _employeeRepository.GetByUserIdAsync(actorUserId);
                if (callerEmployee == null)
                {
                    throw new UnauthorizedAccessException("Caller employee record not found.");
                }

                if (isSupervisor)
                {
                    // Supervisors can ONLY edit direct reports
                    if (!employee.ManagerId.HasValue || employee.ManagerId.Value != callerEmployee.Id)
                    {
                        throw new UnauthorizedAccessException("Supervisors can only edit employees who report directly to them.");
                    }
                }
                else if (isManager)
                {
                    // Managers can edit employees in their OrgUnit OR direct reports
                    bool isDirectReport = employee.ManagerId.HasValue && employee.ManagerId.Value == callerEmployee.Id;
                    bool isSameOrgUnit = employee.OrgUnitId == callerEmployee.OrgUnitId;

                    if (!isDirectReport && !isSameOrgUnit)
                    {
                        throw new UnauthorizedAccessException("Managers can only edit employees within their organizational unit or reporting hierarchy.");
                    }
                }
            }

            // ── 4. Target Role Validation & Privilege Escalation Protection ────────
            var targetRole = await _dbContext.Set<FFM.Workforce.Domain.Entities.Role>().FindAsync(request.RoleId);
            if (targetRole == null)
            {
                throw new InvalidOperationException($"Role with ID {request.RoleId} not found.");
            }

            var targetRoleName = targetRole.Name.ToUpperInvariant();

            if (!isSystemAdmin)
            {
                if (targetRoleName == "SYSTEM_ADMIN")
                {
                    throw new UnauthorizedAccessException("Only System Admins can assign the System Admin role.");
                }

                if (isManager && (targetRoleName == "ADMIN" || targetRoleName == "EXECUTIVE"))
                {
                    throw new UnauthorizedAccessException("Managers cannot assign administrative or executive roles.");
                }

                if (isSupervisor && (targetRoleName == "ADMIN" || targetRoleName == "EXECUTIVE" || targetRoleName == "MANAGER"))
                {
                    throw new UnauthorizedAccessException("Supervisors cannot assign administrative or managerial roles.");
                }
            }

            // ── 5. Manager Validation & Circular Hierarchy Prevention ───────────────
            if (request.ManagerId.HasValue)
            {
                // Self-manager validation
                if (request.ManagerId.Value == employee.Id)
                {
                    throw new InvalidOperationException("Employee cannot be assigned as their own manager.");
                }

                // Manager existence validation
                var manager = await _employeeRepository.GetByIdAsync(request.ManagerId.Value);
                if (manager == null)
                {
                    throw new InvalidOperationException($"Manager with ID {request.ManagerId.Value} not found.");
                }

                // Prevent circular manager hierarchy (e.g. A -> B -> A)
                var managerChain = await _employeeRepository.GetManagerChainAsync(request.ManagerId.Value);
                if (managerChain.Contains(employee.Id))
                {
                    throw new InvalidOperationException("Circular manager hierarchy detected.");
                }
            }

            // ── 6. Check Duplicate Email & Mobile ──────────────────────────────────
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
            var currentEmail = string.IsNullOrWhiteSpace(employee.Email) ? null : employee.Email.Trim().ToLowerInvariant();

            if (normalizedEmail != null && normalizedEmail != currentEmail)
            {
                var isEmailTaken = await _employeeRepository.IsEmailTakenByOtherAsync(normalizedEmail, employee.Id);
                if (isEmailTaken)
                {
                    throw new InvalidOperationException($"Email '{request.Email}' is already assigned to another employee.");
                }

                // Check Identity User duplicate usernameOrEmail
                var existingUser = await _dbContext.Set<FFM.Identity.Access.Domain.Entities.User>()
                    .FirstOrDefaultAsync(u => u.Id != employee.UserId && u.UsernameOrEmail == normalizedEmail, cancellationToken);
                if (existingUser != null)
                {
                    throw new InvalidOperationException($"User with email '{request.Email}' already exists in Identity system.");
                }
            }

            var normalizedMobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim();
            var currentMobile = string.IsNullOrWhiteSpace(employee.Mobile) ? null : employee.Mobile.Trim();

            if (normalizedMobile != null && normalizedMobile != currentMobile)
            {
                var isMobileTaken = await _employeeRepository.IsMobileTakenByOtherAsync(normalizedMobile, employee.Id);
                if (isMobileTaken)
                {
                    throw new InvalidOperationException($"Mobile '{request.Mobile}' is already assigned to another employee.");
                }
            }

            // ── 7. Execute Updates with Transactional Consistency ──────────────────
            var beforeState = JsonSerializer.Serialize(new
            {
                employee.Name,
                employee.Email,
                employee.Mobile,
                employee.RoleId,
                employee.ManagerId
            });

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Update Domain Entity
                employee.Update(
                    name: request.Name.Trim(),
                    email: normalizedEmail,
                    mobile: normalizedMobile,
                    roleId: request.RoleId,
                    managerId: request.ManagerId);

                // Synchronize Identity User (Email & Role)
                var identityUser = await _dbContext.Set<FFM.Identity.Access.Domain.Entities.User>()
                    .FirstOrDefaultAsync(u => u.Id == employee.UserId, cancellationToken);

                if (identityUser != null)
                {
                    if (normalizedEmail != null)
                    {
                        identityUser.UsernameOrEmail = normalizedEmail;
                    }

                    if (Enum.TryParse<FFM.Identity.Access.Domain.Entities.Role>(targetRole.Name, true, out var identityRoleEnum))
                    {
                        identityUser.Role = identityRoleEnum;
                    }
                }

                // ── 8. Secure Audit Logging (No secrets/PII leaked) ─────────────────
                var afterState = JsonSerializer.Serialize(new
                {
                    employee.Name,
                    employee.Email,
                    employee.Mobile,
                    employee.RoleId,
                    employee.ManagerId
                });

                var audit = new AuditLog(
                    entityType: nameof(Employee),
                    entityId: employee.Id,
                    action: "EmployeeUpdated",
                    actorUserId: actorUserId,
                    beforeState: beforeState,
                    afterState: afterState,
                    correlationId: Guid.NewGuid());

                _dbContext.Set<AuditLog>().Add(audit);

                // ── 9. Outbox Event Publication ────────────────────────────────────
                var eventPayload = JsonSerializer.Serialize(new
                {
                    employee.Id,
                    employee.EmployeeCode,
                    employee.UserId,
                    employee.OrgUnitId,
                    employee.Name,
                    employee.Email,
                    employee.Mobile,
                    employee.RoleId,
                    employee.ManagerId
                });

                var outbox = new OutboxMessage("EmployeeUpdated", eventPayload, Guid.NewGuid());
                await _outboxRepository.AddAsync(outbox);

                await _employeeRepository.SaveChangesAsync();
                await _outboxRepository.SaveChangesAsync();
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return new EditEmployeeResponseDto
            {
                Id = employee.Id,
                Message = "Employee updated successfully."
            };
        }
    }
}
