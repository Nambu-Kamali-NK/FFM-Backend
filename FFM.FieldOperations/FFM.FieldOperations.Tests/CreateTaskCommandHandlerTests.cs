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
    public class CreateTaskCommandHandlerTests
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

        [Fact]
        public async Task CreateTask_ValidPayload_Unassigned_InitialStatusIsCreated()
        {
            // Arrange
            var site = CreateActiveSite();
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            _taskRepoMock.Setup(t => t.GenerateNextTaskNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("TSK-202610-0001");

            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var start = DateTime.UtcNow.AddHours(2);
            var end = start.AddHours(3);

            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Annual Server Rack Maintenance",
                Description = "Check power rails and dust filters",
                Priority = "HIGH",
                ClientSiteId = 1,
                AssignedEmployeeId = null,
                ScheduledStart = start,
                ScheduledEnd = end
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TSK-202610-0001", result.TaskNumber);
            Assert.Equal("CREATED", result.Status);
            Assert.Null(result.AssignedEmployeeId);
            Assert.Equal("HIGH", result.Priority);
            Assert.Equal(site.Name, result.ClientSiteName);

            _taskRepoMock.Verify(t => t.AddAsync(It.Is<TaskItem>(item =>
                item.TaskNumber == "TSK-202610-0001" &&
                item.Status == "CREATED" &&
                item.AssignedEmployeeId == null), It.IsAny<CancellationToken>()), Times.Once);
            _taskRepoMock.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateTask_ValidPayload_WithAssignedEmployee_InitialStatusIsAssigned()
        {
            // Arrange
            var site = CreateActiveSite();
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            _taskRepoMock.Setup(t => t.GenerateNextTaskNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("TSK-202610-0002");

            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var start = DateTime.UtcNow.AddHours(1);
            var end = start.AddHours(2);

            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Urgent POS Router Replacement",
                Priority = "URGENT",
                ClientSiteId = 1,
                AssignedEmployeeId = 519,
                ScheduledStart = start,
                ScheduledEnd = end
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TSK-202610-0002", result.TaskNumber);
            Assert.Equal("ASSIGNED", result.Status);
            Assert.Equal(519, result.AssignedEmployeeId);
            Assert.Equal("URGENT", result.Priority);

            _taskRepoMock.Verify(t => t.AddAsync(It.Is<TaskItem>(item =>
                item.TaskNumber == "TSK-202610-0002" &&
                item.Status == "ASSIGNED" &&
                item.AssignedEmployeeId == 519), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateTask_ShortTitle_ThrowsArgumentException()
        {
            // Arrange
            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Fix", // < 5 chars
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Title must be at least 5 characters", ex.Message);
        }

        [Fact]
        public async Task CreateTask_ScheduledEndBeforeStart_ThrowsArgumentException()
        {
            // Arrange
            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var start = DateTime.UtcNow.AddHours(5);
            var end = start.AddHours(-1); // End before start

            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "HVAC Maintenance Check",
                ClientSiteId = 1,
                ScheduledStart = start,
                ScheduledEnd = end
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("ScheduledEnd must be strictly later than ScheduledStart", ex.Message);
        }

        [Fact]
        public async Task CreateTask_ScheduledStartMoreThan24HoursPast_ThrowsArgumentException()
        {
            // Arrange
            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var start = DateTime.UtcNow.AddHours(-25); // > 24 hours in past
            var end = start.AddHours(2);

            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Historical Inspection Log",
                ClientSiteId = 1,
                ScheduledStart = start,
                ScheduledEnd = end
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("ScheduledStart cannot be set more than 24 hours in the past", ex.Message);
        }

        [Fact]
        public async Task CreateTask_InvalidPriority_ThrowsArgumentException()
        {
            // Arrange
            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "ATM Vault Calibration",
                Priority = "CRITICAL", // Not one of LOW, MEDIUM, HIGH, URGENT
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Priority must be one of: LOW, MEDIUM, HIGH, URGENT", ex.Message);
        }

        [Fact]
        public async Task CreateTask_ClientSiteNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _siteRepoMock.Setup(s => s.GetByIdAsync(999))
                .ReturnsAsync((ClientSite?)null);

            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Site Generator Fuel Top-up",
                ClientSiteId = 999,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Client site with ID 999 was not found", ex.Message);
        }

        [Fact]
        public async Task CreateTask_ClientSiteInactive_ThrowsInvalidOperationException()
        {
            // Arrange
            var site = CreateActiveSite();
            site.Deactivate(); // Mark inactive
            _siteRepoMock.Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(site);

            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Closed Site Investigation",
                ClientSiteId = 1,
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("is inactive and cannot accept new tasks", ex.Message);
        }

        [Fact]
        public async Task CreateTask_NegativeAssignedEmployeeId_ThrowsArgumentException()
        {
            // Arrange
            var handler = new CreateTaskCommandHandler(_taskRepoMock.Object, _siteRepoMock.Object);
            var command = new CreateTaskCommand
            {
                ActorUserId = 501,
                Title = "Routine Camera Check",
                ClientSiteId = 1,
                AssignedEmployeeId = -5, // Invalid ID
                ScheduledStart = DateTime.UtcNow.AddHours(1),
                ScheduledEnd = DateTime.UtcNow.AddHours(2)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("AssignedEmployeeId must be a positive integer", ex.Message);
        }
    }
}
