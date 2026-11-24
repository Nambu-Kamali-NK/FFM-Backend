#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using FFM.Workforce.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class EditEmployeeCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepositoryMock;
        private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;
        private readonly WorkforceDbContext _dbContext;
        private readonly EditEmployeeCommandHandler _handler;

        private static readonly Guid TestOrgUnitId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly Guid OtherOrgUnitId = Guid.Parse("99999999-8888-7777-6666-555555555555");

        public EditEmployeeCommandHandlerTests()
        {
            _employeeRepositoryMock = new Mock<IEmployeeRepository>();
            _outboxRepositoryMock = new Mock<IOutboxRepository>();
            _currentUserServiceMock = new Mock<ICurrentUserService>();

            var options = new DbContextOptionsBuilder<WorkforceDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new WorkforceDbContext(options);

            // Seed Roles into in-memory DbContext
            var roles = new[]
            {
                new Role(1, "SYSTEM_ADMIN", "ACTIVE"),
                new Role(2, "ADMIN", "ACTIVE"),
                new Role(3, "EXECUTIVE", "ACTIVE"),
                new Role(4, "MANAGER", "ACTIVE"),
                new Role(5, "SUPERVISOR", "ACTIVE"),
                new Role(6, "EMPLOYEE", "ACTIVE")
            };
            _dbContext.Set<Role>().AddRange(roles);
            _dbContext.SaveChanges();

            _handler = new EditEmployeeCommandHandler(
                _employeeRepositoryMock.Object,
                _outboxRepositoryMock.Object,
                _dbContext,
                _currentUserServiceMock.Object
            );
        }

        [Fact]
        public async Task Handle_EmployeeNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Employee?)null);
            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var command = new EditEmployeeCommand { Id = 999, Name = "Updated Name", RoleId = 6 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("999", ex.Message);
        }

        [Fact]
        public async Task Handle_DeactivatedEmployee_ThrowsInvalidOperationException()
        {
            // Arrange
            var emp = new Employee(10, "EMP10", 10, "Deactivated Emp", 6, TestOrgUnitId, null, "deact@test.com", "1234567890", "System Admin", EmployeeStatus.DEACTIVATED);
            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(emp);
            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var command = new EditEmployeeCommand { Id = 10, Name = "New Name", RoleId = 6 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("deactivated", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Handle_UnauthorizedActorRole_ThrowsUnauthorizedAccessException()
        {
            // Arrange: EMPLOYEE role is not authorized to edit employees
            var emp = new Employee(10, "EMP10", 10, "Target Emp", 6, TestOrgUnitId, null, "target@test.com", "1234567890", "System Admin");
            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(emp);
            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(10);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new EditEmployeeCommand { Id = 10, Name = "New Name", RoleId = 6 };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ManagerEditingAdminProfile_ThrowsUnauthorizedAccessException()
        {
            // Arrange: Target is ADMIN (RoleId = 2). Caller is MANAGER.
            var adminEmp = new Employee(5, "ADMIN1", 5, "Admin User", 2, TestOrgUnitId, null, "admin@test.com", "1234567890", "System Admin");
            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(adminEmp);
            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(20);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            var command = new EditEmployeeCommand { Id = 5, Name = "Hacked Admin", RoleId = 2 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("Admin", ex.Message);
        }

        [Fact]
        public async Task Handle_ManagerEditingOutsideOrgUnitAndNotDirectReport_ThrowsUnauthorizedAccessException()
        {
            // Arrange: Target is in OtherOrgUnitId. Caller Manager is in TestOrgUnitId.
            var targetEmp = new Employee(30, "EMP30", 30, "Outside Emp", 6, OtherOrgUnitId, 999, "out@test.com", "1234567890", "System Admin");
            var managerEmp = new Employee(20, "MGR20", 20, "Manager Emp", 4, TestOrgUnitId, null, "mgr@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);
            _employeeRepositoryMock.Setup(r => r.GetByUserIdAsync(20)).ReturnsAsync(managerEmp);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(20);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Updated Name", RoleId = 6 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("organizational unit", ex.Message);
        }

        [Fact]
        public async Task Handle_SupervisorEditingNonDirectReport_ThrowsUnauthorizedAccessException()
        {
            // Arrange: Target does not report to supervisor (ManagerId = 888, supervisor is 50).
            var targetEmp = new Employee(40, "EMP40", 40, "Peer Emp", 6, TestOrgUnitId, 888, "peer@test.com", "1234567890", "System Admin");
            var supervisorEmp = new Employee(50, "SUP50", 50, "Supervisor Emp", 5, TestOrgUnitId, null, "sup@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(40)).ReturnsAsync(targetEmp);
            _employeeRepositoryMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(supervisorEmp);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(50);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "SUPERVISOR" });

            var command = new EditEmployeeCommand { Id = 40, Name = "Updated Name", RoleId = 6 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("Supervisors can only edit employees who report directly", ex.Message);
        }

        [Fact]
        public async Task Handle_PrivilegeEscalation_ManagerAssigningAdminRole_ThrowsUnauthorizedAccessException()
        {
            // Arrange: Caller is MANAGER. Target is direct report. Manager attempts to change RoleId to 2 (ADMIN).
            var managerEmp = new Employee(20, "MGR20", 20, "Manager Emp", 4, TestOrgUnitId, null, "mgr@test.com", "1234567890", "System Admin");
            var reportEmp = new Employee(30, "EMP30", 30, "Report Emp", 6, TestOrgUnitId, managerEmp.Id, "rep@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(reportEmp);
            _employeeRepositoryMock.Setup(r => r.GetByUserIdAsync(20)).ReturnsAsync(managerEmp);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(20);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Report Emp", RoleId = 2 /* ADMIN */ };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("administrative", ex.Message);
        }

        [Fact]
        public async Task Handle_PrivilegeEscalation_NonSystemAdminAssigningSystemAdminRole_ThrowsUnauthorizedAccessException()
        {
            // Arrange: Caller is ADMIN. Attempts to assign RoleId = 1 (SYSTEM_ADMIN).
            var targetEmp = new Employee(30, "EMP30", 30, "Target Emp", 6, TestOrgUnitId, null, "target@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(2);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Target Emp", RoleId = 1 /* SYSTEM_ADMIN */ };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("System Admin", ex.Message);
        }

        [Fact]
        public async Task Handle_SelfManagerAssignment_ThrowsInvalidOperationException()
        {
            // Arrange: Employee ID = 30, ManagerId = 30
            var targetEmp = new Employee(30, "EMP30", 30, "Target Emp", 6, TestOrgUnitId, null, "target@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "SYSTEM_ADMIN" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Target Emp", RoleId = 6, ManagerId = 30 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("own manager", ex.Message);
        }

        [Fact]
        public async Task Handle_NonExistentManager_ThrowsInvalidOperationException()
        {
            // Arrange: ManagerId = 999 does not exist
            var targetEmp = new Employee(30, "EMP30", 30, "Target Emp", 6, TestOrgUnitId, null, "target@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);
            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Employee?)null);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "SYSTEM_ADMIN" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Target Emp", RoleId = 6, ManagerId = 999 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("Manager with ID 999 not found", ex.Message);
        }

        [Fact]
        public async Task Handle_CircularManagerHierarchy_ThrowsInvalidOperationException()
        {
            // Arrange: Target Emp = 30. Candidate Manager = 40. But Manager 40 reports up to 30!
            var targetEmp = new Employee(30, "EMP30", 30, "Emp 30", 6, TestOrgUnitId, null, "e30@test.com", "1234567890", "System Admin");
            var mgrEmp = new Employee(40, "EMP40", 40, "Emp 40", 4, TestOrgUnitId, null, "e40@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);
            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(40)).ReturnsAsync(mgrEmp);
            _employeeRepositoryMock.Setup(r => r.GetManagerChainAsync(40)).ReturnsAsync(new List<int> { 40, 50, 30 }); // 30 is in chain!

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "SYSTEM_ADMIN" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Emp 30", RoleId = 6, ManagerId = 40 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("Circular manager hierarchy", ex.Message);
        }

        [Fact]
        public async Task Handle_DuplicateEmail_ThrowsInvalidOperationException()
        {
            // Arrange: Email 'duplicate@test.com' taken by another employee
            var targetEmp = new Employee(30, "EMP30", 30, "Emp 30", 6, TestOrgUnitId, null, "original@test.com", "1234567890", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);
            _employeeRepositoryMock.Setup(r => r.IsEmailTakenByOtherAsync("duplicate@test.com", 30)).ReturnsAsync(true);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Emp 30", RoleId = 6, Email = "duplicate@test.com" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("already assigned to another employee", ex.Message);
        }

        [Fact]
        public async Task Handle_DuplicateMobile_ThrowsInvalidOperationException()
        {
            // Arrange: Mobile '9876543210' taken by another employee
            var targetEmp = new Employee(30, "EMP30", 30, "Emp 30", 6, TestOrgUnitId, null, "e@test.com", "1111111111", "System Admin");

            _employeeRepositoryMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(targetEmp);
            _employeeRepositoryMock.Setup(r => r.IsMobileTakenByOtherAsync("9876543210", 30)).ReturnsAsync(true);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var command = new EditEmployeeCommand { Id = 30, Name = "Emp 30", RoleId = 6, Mobile = "9876543210" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Contains("already assigned to another employee", ex.Message);
        }
    }
}
