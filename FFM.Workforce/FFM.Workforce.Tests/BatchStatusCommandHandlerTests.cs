using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class BatchStatusCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

        public BatchStatusCommandHandlerTests()
        {
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });
        }

        [Fact]
        public async Task Handle_DeactivateSingleEmployee_DeactivatesStatus()
        {
            var handler = new DeactivateEmployeeCommandHandler(_employeeRepoMock.Object, _currentUserServiceMock.Object);
            var emp = new Employee(1, "EMP1", 10, "Test", 1, Guid.NewGuid(), null, "t@test.com", "9876543210", "System Admin");
            
            _employeeRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(emp);

            var result = await handler.Handle(new DeactivateEmployeeCommand { EmployeeId = 1, ActorUserId = 100 }, CancellationToken.None);

            Assert.True(result);
            Assert.Equal(EmployeeStatus.DEACTIVATED, emp.Status);
            _employeeRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_BatchActivate_ActivatesMultipleEmployees()
        {
            var handler = new BatchActivateEmployeesCommandHandler(_employeeRepoMock.Object, _currentUserServiceMock.Object);
            var emp1 = new Employee(1, "EMP1", 10, "User 1", 1, Guid.NewGuid(), null, "u1@test.com", "9876543210", "System Admin");
            var emp2 = new Employee(2, "EMP2", 11, "User 2", 1, Guid.NewGuid(), null, "u2@test.com", "9876543211", "System Admin");

            _employeeRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(emp1);
            _employeeRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(emp2);

            var cmd = new BatchActivateEmployeesCommand { EmployeeIds = new List<int> { 1, 2 }, ActorUserId = 100 };
            var count = await handler.Handle(cmd, CancellationToken.None);

            Assert.Equal(2, count);
            Assert.Equal(EmployeeStatus.ACTIVE, emp1.Status);
            Assert.Equal(EmployeeStatus.ACTIVE, emp2.Status);
            _employeeRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_BatchDeactivate_DeactivatesMultipleEmployees()
        {
            var handler = new BatchDeactivateEmployeesCommandHandler(_employeeRepoMock.Object, _currentUserServiceMock.Object);
            var emp1 = new Employee(1, "EMP1", 10, "User 1", 1, Guid.NewGuid(), null, "u1@test.com", "9876543210", "System Admin");
            var emp2 = new Employee(2, "EMP2", 11, "User 2", 1, Guid.NewGuid(), null, "u2@test.com", "9876543211", "System Admin");
            emp1.Activate();
            emp2.Activate();

            _employeeRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(emp1);
            _employeeRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(emp2);

            var cmd = new BatchDeactivateEmployeesCommand { EmployeeIds = new List<int> { 1, 2 }, ActorUserId = 100 };
            var count = await handler.Handle(cmd, CancellationToken.None);

            Assert.Equal(2, count);
            Assert.Equal(EmployeeStatus.DEACTIVATED, emp1.Status);
            Assert.Equal(EmployeeStatus.DEACTIVATED, emp2.Status);
            _employeeRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }
    }
}
