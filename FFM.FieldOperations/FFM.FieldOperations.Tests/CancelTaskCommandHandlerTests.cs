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
    public class CancelTaskCommandHandlerTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();
        private readonly Mock<IClientSiteRepository> _siteRepoMock = new();

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

        private TaskItem CreateSampleTask(string status = "CREATED", int? assignedEmployeeId = null)
        {
            var task = new TaskItem(
                "TSK-202610-0001",
                "Quarterly ATM Maintenance",
                "Check cash sensor and thermal printer",
                "HIGH",
                1,
                assignedEmployeeId,
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddHours(4),
                501);

            if (status == "ASSIGNED")
            {
                task.Assign(assignedEmployeeId ?? 10);
            }
            else if (status == "EN_ROUTE")
            {
                task.Assign(assignedEmployeeId ?? 10);
                task.UpdateStatus("EN_ROUTE");
            }
            else if (status == "CHECKED_IN")
            {
                task.Assign(assignedEmployeeId ?? 10);
                task.UpdateStatus("CHECKED_IN");
            }
            else if (status == "COMPLETED")
            {
                task.Assign(assignedEmployeeId ?? 10);
                task.UpdateStatus("COMPLETED");
            }
            else if (status == "CANCELLED")
            {
                task.Cancel("Initial cancellation reason for setup");
            }

            return task;
        }

        [Fact]
        public async Task CancelTask_CreatedStatus_SucceedsAndSetsStatusToCancelled()
        {
            // Arrange
            var task = CreateSampleTask("CREATED");
            var site = CreateActiveSite();
            _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 1,
                Reason = "Customer requested postponement due to branch renovation.",
                ActorUserId = 501
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("CANCELLED", result.Status);
            Assert.Equal(command.Reason, result.CancellationReason);
            Assert.NotNull(result.CancelledAt);
            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelTask_AssignedStatus_SucceedsAndCancelsScheduledTask()
        {
            // Arrange
            var task = CreateSampleTask("ASSIGNED", 15);
            var site = CreateActiveSite();
            _taskRepoMock.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 2,
                Reason = "Parts are out of stock; rescheduling required.",
                ActorUserId = 501
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("CANCELLED", result.Status);
            Assert.Equal(command.Reason, result.CancellationReason);
        }

        [Fact]
        public async Task CancelTask_EnRouteStatus_SucceedsAndRecallsTechnician()
        {
            // Arrange
            var task = CreateSampleTask("EN_ROUTE", 15);
            var site = CreateActiveSite();
            _taskRepoMock.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 3,
                Reason = "Severe flooding along route; recall technician immediately.",
                ActorUserId = 501
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("CANCELLED", result.Status);
        }

        [Fact]
        public async Task CancelTask_CheckedInStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            var task = CreateSampleTask("CHECKED_IN", 15);
            _taskRepoMock.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 4,
                Reason = "Attempt to cancel active job",
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot cancel a task that is in 'CHECKED_IN' status", ex.Message);
        }

        [Fact]
        public async Task CancelTask_CompletedStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            var task = CreateSampleTask("COMPLETED", 15);
            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 5,
                Reason = "Attempt to cancel finished job",
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot cancel a task that is in 'COMPLETED' status", ex.Message);
        }

        [Fact]
        public async Task CancelTask_AlreadyCancelledStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            var task = CreateSampleTask("CANCELLED");
            _taskRepoMock.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 6,
                Reason = "Attempt to re-cancel an already voided task",
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cannot cancel a task that is in 'CANCELLED' status", ex.Message);
        }

        [Fact]
        public async Task CancelTask_NonExistentTaskId_ThrowsKeyNotFoundException()
        {
            // Arrange
            _taskRepoMock.Setup(r => r.GetByIdAsync(9999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TaskItem?)null);

            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 9999,
                Reason = "Valid cancellation reason string",
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Task with ID 9999 was not found", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task CancelTask_InvalidTaskId_ThrowsArgumentException(int invalidTaskId)
        {
            // Arrange
            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = invalidTaskId,
                Reason = "Valid cancellation reason",
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Valid TaskId is required", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("1234")] // 4 characters
        public async Task CancelTask_ReasonTooShort_ThrowsArgumentException(string shortReason)
        {
            // Arrange
            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 1,
                Reason = shortReason,
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cancellation reason must be provided (at least 5 characters)", ex.Message);
        }

        [Fact]
        public async Task CancelTask_ReasonExceeding500Chars_ThrowsArgumentException()
        {
            // Arrange
            var handler = new CancelTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CancelTaskCommand
            {
                TaskId = 1,
                Reason = new string('A', 501),
                ActorUserId = 501
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Cancellation reason cannot exceed 500 characters", ex.Message);
        }
    }
}
