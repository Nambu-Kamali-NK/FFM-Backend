using System;
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
    public class AssignTaskCommandHandlerTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();
        private readonly Mock<IClientSiteRepository> _siteRepoMock = new();

        private ClientSite CreateActiveSite()
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

        private TaskItem CreateTask(string status = "CREATED", int? assignedEmployeeId = null)
        {
            var task = new TaskItem(
                "TSK-202610-0001",
                "Quarterly POS Hardware Maintenance",
                "Inspect terminal, replace battery backup",
                "HIGH",
                1,
                assignedEmployeeId,
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddHours(4),
                12);

            if (status != "CREATED" && status != "ASSIGNED")
            {
                task.UpdateStatus(status);
            }
            return task;
        }

        [Fact]
        public async Task AssignTask_UnassignedTask_TransitionsToAssigned()
        {
            // Arrange
            var task = CreateTask(status: "CREATED", assignedEmployeeId: null);
            var site = CreateActiveSite();

            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = 8,
                ActorUserId = 12
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ASSIGNED", result.Status);
            Assert.Equal(8, result.AssignedEmployeeId);
            Assert.Equal(site.Name, result.ClientSiteName);

            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AssignTask_AlreadyAssignedTask_ReassignsToNewTechnician()
        {
            // Arrange
            var task = CreateTask(status: "ASSIGNED", assignedEmployeeId: 8);
            var site = CreateActiveSite();

            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = 14,
                ActorUserId = 12
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ASSIGNED", result.Status);
            Assert.Equal(14, result.AssignedEmployeeId);

            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AssignTask_TaskNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _taskRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TaskItem?)null);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 999,
                AssignedEmployeeId = 8,
                ActorUserId = 12
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Task with ID 999 was not found", ex.Message);
        }

        [Fact]
        public async Task AssignTask_InvalidTaskId_ThrowsArgumentException()
        {
            // Arrange
            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 0,
                AssignedEmployeeId = 8,
                ActorUserId = 12
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Valid TaskId is required", ex.Message);
        }

        [Fact]
        public async Task AssignTask_InvalidEmployeeId_ThrowsArgumentException()
        {
            // Arrange
            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = -1,
                ActorUserId = 12
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("AssignedEmployeeId must be a positive integer", ex.Message);
        }

        [Fact]
        public async Task AssignTask_TaskCheckedIn_ThrowsInvalidOperationException()
        {
            // Arrange
            var task = CreateTask(status: "ASSIGNED", assignedEmployeeId: 8);
            task.UpdateStatus("CHECKED_IN");

            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = 14,
                ActorUserId = 12
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot reassign a task that is in 'CHECKED_IN' status", ex.Message);
        }

        [Fact]
        public async Task AssignTask_TaskCompleted_ThrowsInvalidOperationException()
        {
            // Arrange
            var task = CreateTask(status: "ASSIGNED", assignedEmployeeId: 8);
            task.UpdateStatus("COMPLETED");

            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = 14,
                ActorUserId = 12
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot reassign a task that is in 'COMPLETED' status", ex.Message);
        }

        [Fact]
        public async Task AssignTask_TaskCancelled_ThrowsInvalidOperationException()
        {
            // Arrange
            var task = CreateTask(status: "CREATED", assignedEmployeeId: null);
            task.Cancel("Customer requested cancellation");

            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new AssignTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new AssignTaskCommand
            {
                TaskId = 1,
                AssignedEmployeeId = 8,
                ActorUserId = 12
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot reassign a task that is in 'CANCELLED' status", ex.Message);
        }
    }
}
