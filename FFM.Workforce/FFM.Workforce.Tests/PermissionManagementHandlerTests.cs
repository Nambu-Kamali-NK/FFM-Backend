using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class PermissionManagementHandlerTests
    {
        private readonly Mock<IPermissionRepository> _permissionRepoMock = new();
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();

        private Employee CreateTestEmployee(int id, int userId, EmployeeStatus status, Guid orgUnitId, int? managerId = null)
        {
            return new Employee(
                id,
                $"EMP{id:D3}",
                userId,
                $"Employee {id}",
                1,
                orgUnitId,
                managerId,
                $"emp{id}@test.com",
                "9876543210",
                "SYSTEM",
                status);
        }

        [Fact]
        public async Task SubmitPermissionRequest_Successful_CreatesPendingPermission()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);
            _permissionRepoMock.Setup(r => r.HasOverlappingLeaveOnDateAsync(1, It.IsAny<DateTime>())).ReturnsAsync(false);
            _permissionRepoMock.Setup(r => r.HasOverlappingPermissionAsync(1, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null)).ReturnsAsync(false);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(2),
                StartTime = "14:00:00",
                EndTime = "16:00:00",
                Reason = "Doctor visit"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PENDING", result.Status);
            Assert.Equal(2.0m, result.DurationHours);
            Assert.Equal("14:00:00", result.StartTime);
            Assert.Equal("16:00:00", result.EndTime);
            _permissionRepoMock.Verify(r => r.AddPermissionRequestAsync(It.IsAny<PermissionRequest>()), Times.Once);
            _permissionRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SubmitPermissionRequest_InactiveEmployee_ThrowsInvalidOperationException()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.DEACTIVATED, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(1),
                StartTime = "10:00:00",
                EndTime = "12:00:00",
                Reason = "Personal errand"
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task SubmitPermissionRequest_EndTimeBeforeStartTime_ThrowsArgumentException()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(1),
                StartTime = "16:00:00",
                EndTime = "14:00:00",
                Reason = "Personal errand"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task SubmitPermissionRequest_DurationExceeds4Hours_ThrowsInvalidOperationException()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(1),
                StartTime = "09:00:00",
                EndTime = "14:00:00", // 5 hours > 4 hours
                Reason = "Extended absence"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("cannot exceed 4.0 hours", ex.Message);
        }

        [Fact]
        public async Task SubmitPermissionRequest_DurationLessThan30Minutes_ThrowsArgumentException()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(1),
                StartTime = "09:00:00",
                EndTime = "09:15:00", // 15 mins < 30 mins
                Reason = "Coffee break"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("at least 30 minutes", ex.Message);
        }

        [Fact]
        public async Task SubmitPermissionRequest_OverlappingLeave_ThrowsInvalidOperationException()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);
            _permissionRepoMock.Setup(r => r.HasOverlappingLeaveOnDateAsync(1, It.IsAny<DateTime>())).ReturnsAsync(true);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(1),
                StartTime = "10:00:00",
                EndTime = "12:00:00",
                Reason = "Bank visit"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("leave request already exists covering this date", ex.Message);
        }

        [Fact]
        public async Task SubmitPermissionRequest_OverlappingPermission_ThrowsInvalidOperationException()
        {
            // Arrange
            var emp = CreateTestEmployee(1, 101, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101)).ReturnsAsync(emp);
            _permissionRepoMock.Setup(r => r.HasOverlappingLeaveOnDateAsync(1, It.IsAny<DateTime>())).ReturnsAsync(false);
            _permissionRepoMock.Setup(r => r.HasOverlappingPermissionAsync(1, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null)).ReturnsAsync(true);

            var handler = new SubmitPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object);
            var command = new SubmitPermissionRequestCommand
            {
                ActorUserId = 101,
                PermissionDate = DateTime.UtcNow.Date.AddDays(1),
                StartTime = "10:00:00",
                EndTime = "12:00:00",
                Reason = "Bank visit"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("permission request already exists for this time window", ex.Message);
        }

        [Fact]
        public async Task DecidePermissionRequest_EmployeeRole_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new List<string> { "EMPLOYEE" });

            var handler = new DecidePermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);
            var command = new DecidePermissionRequestCommand
            {
                RequestId = 1,
                ActorUserId = 101,
                Decision = "APPROVED"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task DecidePermissionRequest_SelfApproval_ThrowsInvalidOperationException()
        {
            // Arrange
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new List<string> { "MANAGER" });
            var managerEmp = CreateTestEmployee(5, 501, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(501)).ReturnsAsync(managerEmp);

            var permission = new PermissionRequest(5, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0), 2.0m, "Self errand");
            _permissionRepoMock.Setup(r => r.GetPermissionRequestByIdAsync(1)).ReturnsAsync(permission);

            var handler = new DecidePermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);
            var command = new DecidePermissionRequestCommand
            {
                RequestId = 1,
                ActorUserId = 501,
                Decision = "APPROVED"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Self-approval", ex.Message);
        }

        [Fact]
        public async Task DecidePermissionRequest_ManagerApproves_SetsStatusToApproved()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new List<string> { "MANAGER" });
            var managerEmp = CreateTestEmployee(10, 501, EmployeeStatus.ACTIVE, orgUnitId);
            var subordinate = CreateTestEmployee(2, 202, EmployeeStatus.ACTIVE, orgUnitId, managerId: 10);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(501)).ReturnsAsync(managerEmp);
            _employeeRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(subordinate);

            var permission = new PermissionRequest(2, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0), 2.0m, "Dentist");
            _permissionRepoMock.Setup(r => r.GetPermissionRequestByIdAsync(1)).ReturnsAsync(permission);

            var handler = new DecidePermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);
            var command = new DecidePermissionRequestCommand
            {
                RequestId = 1,
                ActorUserId = 501,
                Decision = "APPROVED",
                Comments = "Approved by Manager"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("APPROVED", result.Status);
            Assert.Equal(10, result.ApproverId);
            Assert.Equal("Approved by Manager", result.DecisionComments);
            _permissionRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DecidePermissionRequest_ManagerRejects_SetsStatusToRejected()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new List<string> { "MANAGER" });
            var managerEmp = CreateTestEmployee(10, 501, EmployeeStatus.ACTIVE, orgUnitId);
            var subordinate = CreateTestEmployee(2, 202, EmployeeStatus.ACTIVE, orgUnitId, managerId: 10);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(501)).ReturnsAsync(managerEmp);
            _employeeRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(subordinate);

            var permission = new PermissionRequest(2, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0), 2.0m, "Dentist");
            _permissionRepoMock.Setup(r => r.GetPermissionRequestByIdAsync(1)).ReturnsAsync(permission);

            var handler = new DecidePermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);
            var command = new DecidePermissionRequestCommand
            {
                RequestId = 1,
                ActorUserId = 501,
                Decision = "REJECTED",
                Comments = "Insufficient shift coverage"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("REJECTED", result.Status);
            Assert.Equal(10, result.ApproverId);
            Assert.Equal("Insufficient shift coverage", result.DecisionComments);
            _permissionRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CancelPermissionRequest_EmployeeCancelsOwnPendingRequest_SetsStatusToCancelled()
        {
            // Arrange
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new List<string> { "EMPLOYEE" });
            var emp = CreateTestEmployee(2, 202, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(202)).ReturnsAsync(emp);

            var permission = new PermissionRequest(2, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0), 2.0m, "Dentist");
            _permissionRepoMock.Setup(r => r.GetPermissionRequestByIdAsync(1)).ReturnsAsync(permission);

            var handler = new CancelPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);
            var command = new CancelPermissionRequestCommand
            {
                RequestId = 1,
                ActorUserId = 202
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("CANCELLED", result.Status);
            _permissionRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CancelPermissionRequest_NonOwnerNonAdmin_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            _currentUserMock.Setup(u => u.GetRoles()).Returns(new List<string> { "EMPLOYEE" });
            var stranger = CreateTestEmployee(3, 303, EmployeeStatus.ACTIVE, Guid.NewGuid());
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(303)).ReturnsAsync(stranger);

            var permission = new PermissionRequest(2, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(14, 0, 0), new TimeSpan(16, 0, 0), 2.0m, "Dentist");
            _permissionRepoMock.Setup(r => r.GetPermissionRequestByIdAsync(1)).ReturnsAsync(permission);

            var handler = new CancelPermissionRequestCommandHandler(_permissionRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);
            var command = new CancelPermissionRequestCommand
            {
                RequestId = 1,
                ActorUserId = 303
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("cancel your own permission requests", ex.Message);
        }
    }
}
