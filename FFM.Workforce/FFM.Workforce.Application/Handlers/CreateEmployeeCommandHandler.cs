using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFM.Workforce.Application.Handlers
{
    /// <summary>
    /// Handles the creation of a new employee.
    /// Business rules:
    ///   * Permission check (SYSTEM_ADMIN, ADMIN, MANAGER)
    ///   * Uniqueness of EmployeeCode
    ///   * UserId is NOT required — employee can be created without an Identity account (null at creation).
    ///   * OrgUnitId is determined automatically from the active default OrgUnit — NOT supplied by the client.
    ///   * If no active default OrgUnit exists, creation fails with a clear business error.
    ///   * Transactional consistency — employee, audit log and outbox persisted in a single transaction.
    ///   * Emits an EmployeeCreated domain event via the outbox.
    /// </summary>
    public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, int>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOutboxRepository _outboxRepository;
        private readonly IOrgUnitRepository _orgUnitRepository;
        private readonly DbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;

        public CreateEmployeeCommandHandler(
            IEmployeeRepository employeeRepository,
            IOutboxRepository outboxRepository,
            IOrgUnitRepository orgUnitRepository,
            DbContext dbContext,
            ICurrentUserService currentUserService)
        {
            _employeeRepository = employeeRepository;
            _outboxRepository = outboxRepository;
            _orgUnitRepository = orgUnitRepository;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
        }

        public async Task<int> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            // ----- Authorization -----
            var actorRoles = _currentUserService.GetRoles();
            if (!actorRoles.Contains("SYSTEM_ADMIN") &&
                !actorRoles.Contains("ADMIN") &&
                !actorRoles.Contains("MANAGER"))
            {
                throw new UnauthorizedAccessException("Actor does not have permission to create employees.");
            }

            // ----- Business rules -----

            // 1. EmployeeCode uniqueness
            var existingByCode = await _employeeRepository.GetByEmployeeCodeAsync(request.EmployeeCode);
            if (existingByCode != null)
                throw new InvalidOperationException($"EmployeeCode '{request.EmployeeCode}' already exists.");

            // 2. Resolve the default active OrgUnit
            var defaultOrgUnit = await _orgUnitRepository.GetDefaultActiveAsync();
            if (defaultOrgUnit == null)
                throw new InvalidOperationException(
                    "No active default OrgUnit is configured.");

            // 3. Ensure Role exists and get it for Identity mapping
            var workforceRole = await _dbContext.Set<Role>().FindAsync(request.RoleId);
            if (workforceRole == null)
                throw new InvalidOperationException($"Role with ID {request.RoleId} not found.");

            // Map Workforce Role Name to Identity Role Enum
            if (!Enum.TryParse<FFM.Identity.Access.Domain.Entities.Role>(workforceRole.Name, true, out var identityRole))
            {
                identityRole = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE;
            }

            // Role hierarchy authorization:
            // - SYSTEM_ADMIN can assign any role.
            // - ADMIN can assign any role except SYSTEM_ADMIN.
            // - MANAGER can only assign subordinate roles (EMPLOYEE, SUPERVISOR, MANAGER). Cannot assign SYSTEM_ADMIN, ADMIN, or EXECUTIVE.
            var isSystemAdmin = actorRoles.Contains("SYSTEM_ADMIN");
            var isAdmin = actorRoles.Contains("ADMIN");
            var isManager = actorRoles.Contains("MANAGER");

            if (!isSystemAdmin)
            {
                if (identityRole == FFM.Identity.Access.Domain.Entities.Role.SYSTEM_ADMIN)
                {
                    throw new UnauthorizedAccessException("Only System Admins can assign the System Admin role.");
                }

                if (!isAdmin && isManager)
                {
                    if (identityRole == FFM.Identity.Access.Domain.Entities.Role.ADMIN ||
                        identityRole == FFM.Identity.Access.Domain.Entities.Role.EXECUTIVE)
                    {
                        throw new UnauthorizedAccessException("Managers cannot assign administrative or executive roles.");
                    }
                }
            }

            // 4. Create Identity User
            var usernameOrEmail = string.IsNullOrWhiteSpace(request.Email) 
                ? request.EmployeeCode.ToLowerInvariant() 
                : request.Email.Trim().ToLowerInvariant();

            var existingUser = await _dbContext.Set<FFM.Identity.Access.Domain.Entities.User>()
                .FirstOrDefaultAsync(u => u.UsernameOrEmail == usernameOrEmail, cancellationToken);
            
            if (existingUser != null)
                throw new InvalidOperationException($"User with email/username '{usernameOrEmail}' already exists.");

            var identityUser = new FFM.Identity.Access.Domain.Entities.User
            {
                UsernameOrEmail = usernameOrEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = identityRole,
                Status = FFM.Identity.Access.Domain.Entities.AccountStatus.Active, // Active so they can login immediately
                MfaEnabled = false,
                FailedAttempts = 0
            };

            int employeeId = 0;

            // ----- Transactional persistence -----
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Save User first to generate the auto-increment integer ID
                _dbContext.Set<FFM.Identity.Access.Domain.Entities.User>().Add(identityUser);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Determine CreatedBy display string (Name, Role, or explicit string)
                string createdByString;
                if (!string.IsNullOrWhiteSpace(request.CreatedBy))
                {
                    createdByString = request.CreatedBy;
                }
                else if (isSystemAdmin)
                {
                    createdByString = "System Admin";
                }
                else
                {
                    var actorEmployee = await _employeeRepository.GetByUserIdAsync(request.ActorUserId);
                    if (actorEmployee != null && !string.IsNullOrWhiteSpace(actorEmployee.Name))
                    {
                        createdByString = actorEmployee.Name;
                    }
                    else
                    {
                        var actorUser = await _dbContext.Set<FFM.Identity.Access.Domain.Entities.User>()
                            .FindAsync(new object[] { request.ActorUserId }, cancellationToken);
                        if (actorUser != null && !string.IsNullOrWhiteSpace(actorUser.UsernameOrEmail))
                        {
                            createdByString = actorUser.UsernameOrEmail;
                        }
                        else
                        {
                            createdByString = actorRoles.FirstOrDefault() ?? "System Admin";
                        }
                    }
                }

                int? effectiveManagerId = (request.ManagerId.HasValue && request.ManagerId.Value > 0) ? request.ManagerId : null;

                // 5. Create entity — UserId is mapped to the new Identity User.
                var employee = new Employee(
                    employeeCode: request.EmployeeCode,
                    userId: identityUser.Id,
                    name: request.Name,
                    roleId: request.RoleId,
                    orgUnitId: defaultOrgUnit.Id,
                    managerId: effectiveManagerId,
                    email: request.Email,
                    mobile: request.Mobile,
                    createdBy: createdByString);

                await _employeeRepository.AddAsync(employee);
                await _employeeRepository.SaveChangesAsync();

                employeeId = employee.Id;

                // Audit log
                var audit = new AuditLog(
                    entityType: nameof(Employee),
                    entityId: employee.Id,
                    action: "Created",
                    actorUserId: request.ActorUserId,
                    beforeState: null,
                    afterState: null,
                    correlationId: Guid.NewGuid());
                _dbContext.Set<AuditLog>().Add(audit);

                // Outbox event
                var eventPayload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    employee.Id,
                    employee.EmployeeCode,
                    employee.UserId,
                    employee.OrgUnitId,
                    employee.Name
                });
                var outbox = new OutboxMessage("EmployeeCreated", eventPayload, Guid.NewGuid());
                await _outboxRepository.AddAsync(outbox);

                await _outboxRepository.SaveChangesAsync();
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return employeeId;
        }
    }
}
