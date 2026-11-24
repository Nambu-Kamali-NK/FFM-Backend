using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class UpdateTaskStatusCommandHandlerTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();
        private readonly Mock<IClientSiteRepository> _siteRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

        private ClientSite CreateActiveSite(int siteId = 1)
        {
            return new ClientSite(
                "SITE-BLR-001",
                "HDFC Bank Regional Office",
                12.9715987m,
                77.5945627m,
                "42 MG Road, Bengaluru, Karnataka",
                501,
                150,
                "Ramesh Kumar",
                "+919876543210",
                "ramesh@hdfcbank.com",
                "Bengaluru",
                "Karnataka",
                "560001");
        }

        private TaskItem CreateSampleTask(string status = "ASSIGNED", int? assignedEmployeeId = 12)
        {
            var task = new TaskItem(
                "TSK-202610-0001",
                "Quarterly ATM Maintenance",
                "Check cash sensor and thermal printer",
                "HIGH",
                1,
                status == "CREATED" ? null : (assignedEmployeeId ?? 12),
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddHours(4),
                501);

            if (status == "EN_ROUTE")
            {
                task.StartTravel();
            }
            else if (status == "CHECKED_IN")
            {
                task.UpdateStatus("CHECKED_IN");
            }
            else if (status == "COMPLETED")
            {
                task.UpdateStatus("COMPLETED");
            }
            else if (status == "CANCELLED")
            {
                task.Cancel("Pre-cancelled reason");
            }

            return task;
        }

        [Fact]
        public async Task Handle_ValidTransition_FromAssignedToEnRoute_AsAssignedEmployee_Success()
        {
            // Arrange
            var task = CreateSampleTask("ASSIGNED", assignedEmployeeId: 12);
            var site = CreateActiveSite();

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(site);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                Notes = "Departing now for client site.",
                ActorUserId = 12,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("EN_ROUTE", result.Status);
            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("ADMIN")]
        [InlineData("SYSTEM_ADMIN")]
        [InlineData("MANAGER")]
        [InlineData("SUPERVISOR")]
        public async Task Handle_ValidTransition_AsManagementRole_CanUpdateAnyEmployeeTask(string role)
        {
            // Arrange: task assigned to employee 12, but actor is management user 999
            var task = CreateSampleTask("ASSIGNED", assignedEmployeeId: 12);
            var site = CreateActiveSite();

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(site);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(999);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { role });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 999,
                ActorRoles = new[] { role }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("EN_ROUTE", result.Status);
            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Idempotent_WhenAlreadyEnRoute_ReturnsSuccessWithoutError()
        {
            // Arrange
            var task = CreateSampleTask("EN_ROUTE", assignedEmployeeId: 12);
            var site = CreateActiveSite();

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(site);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 12,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("EN_ROUTE", result.Status);
        }

        [Fact]
        public async Task Handle_BOLA_EmployeeUpdatingAnotherEmployeesTask_ThrowsUnauthorizedAccessException()
        {
            // Arrange: task assigned to employee 12, caller is employee 15
            var task = CreateSampleTask("ASSIGNED", assignedEmployeeId: 12);

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(15);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 15,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Access denied", ex.Message);
        }

        [Fact]
        public async Task Handle_BOLA_EmployeeUpdatingUnassignedTask_ThrowsUnauthorizedAccessException()
        {
            // Arrange: unassigned task (status CREATED, AssignedEmployeeId = null)
            var task = CreateSampleTask("CREATED", assignedEmployeeId: null);

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(15);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 15,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_StateGuard_WhenTaskIsCreated_ThrowsInvalidOperationException()
        {
            // Arrange: unassigned task (status CREATED), updated by Admin
            var task = CreateSampleTask("CREATED", assignedEmployeeId: null);

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Cannot start travel on an unassigned task", ex.Message);
        }

        [Fact]
        public async Task Handle_StateGuard_WhenTaskIsCheckedIn_ThrowsInvalidOperationException()
        {
            var task = CreateSampleTask("CHECKED_IN", assignedEmployeeId: 12);

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 12,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Cannot transition task to EN_ROUTE from current status 'CHECKED_IN'", ex.Message);
        }

        [Fact]
        public async Task Handle_StateGuard_WhenTaskIsCompleted_ThrowsInvalidOperationException()
        {
            var task = CreateSampleTask("COMPLETED", assignedEmployeeId: 12);

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Cannot transition task to EN_ROUTE from current status 'COMPLETED'", ex.Message);
        }

        [Fact]
        public async Task Handle_StateGuard_WhenTaskIsCancelled_ThrowsInvalidOperationException()
        {
            var task = CreateSampleTask("CANCELLED", assignedEmployeeId: 12);

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(1);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "ADMIN" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Cannot transition task to EN_ROUTE from current status 'CANCELLED'", ex.Message);
        }

        [Fact]
        public async Task Handle_TaskNotFound_ThrowsKeyNotFoundException()
        {
            _taskRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TaskItem?)null);

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 999,
                Status = "EN_ROUTE",
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Task with ID 999 was not found", ex.Message);
        }

        [Fact]
        public async Task Handle_ValidTransition_FromAssignedToCompleted_Success()
        {
            // Arrange
            var task = CreateSampleTask("ASSIGNED", assignedEmployeeId: 12);
            var site = CreateActiveSite();

            _taskRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(site);

            _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(12);
            _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "COMPLETED",
                Notes = "Work finished on site.",
                ActorUserId = 12,
                ActorRoles = new[] { "EMPLOYEE" }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("COMPLETED", result.Status);
            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("UNKNOWN")]
        [InlineData("DRIVING")]
        [InlineData("CHECKED_IN")]
        [InlineData("INVALID")]
        public async Task Handle_InvalidStatusValue_ThrowsArgumentException(string invalidStatus)
        {
            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = invalidStatus,
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Permitted status for this endpoint is 'EN_ROUTE' or 'COMPLETED'", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Handle_InvalidTaskId_ThrowsArgumentException(int invalidTaskId)
        {
            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = invalidTaskId,
                Status = "EN_ROUTE",
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NotesExceeding500Characters_ThrowsArgumentException()
        {
            var handler = new UpdateTaskStatusCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _currentUserServiceMock.Object);

            var command = new UpdateTaskStatusCommand
            {
                TaskId = 105,
                Status = "EN_ROUTE",
                Notes = new string('A', 501),
                ActorUserId = 1,
                ActorRoles = new[] { "ADMIN" }
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                handler.Handle(command, CancellationToken.None));

            Assert.Contains("Notes cannot exceed 500 characters", ex.Message);
        }
    }
}
