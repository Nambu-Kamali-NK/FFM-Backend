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
    public class ActivateEmployeeCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
        private readonly ActivateEmployeeCommandHandler _handler;

        public ActivateEmployeeCommandHandlerTests()
        {
            _handler = new ActivateEmployeeCommandHandler(_employeeRepoMock.Object, _currentUserServiceMock.Object);
        }

        [Fact]
        public async Task Handle_ValidPendingEmployee_ActivatesStatusSuccessfully()
        {
            // Arrange
            var employee = new Employee(1, "EMP1", 10, "Test Employee", 1, Guid.NewGuid(), null, "t@test.com", "9876543210", "System Admin");
            
            _employeeRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(employee);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var command = new ActivateEmployeeCommand { EmployeeId = 1, ActorUserId = 100 };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(EmployeeStatus.ACTIVE, employee.Status);
            _employeeRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_UnauthorizedActor_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "FIELD_EMPLOYEE" });
            var command = new ActivateEmployeeCommand { EmployeeId = 1, ActorUserId = 100 };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NonExistentEmployee_ThrowsKeyNotFoundException()
        {
            // Arrange
            _employeeRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Employee?)null);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });
            var command = new ActivateEmployeeCommand { EmployeeId = 999, ActorUserId = 100 };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        }
    }
}
