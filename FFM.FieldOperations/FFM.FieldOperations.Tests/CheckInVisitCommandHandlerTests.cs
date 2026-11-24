using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Handlers;
using FFM.FieldOperations.Application.Interfaces;
using FFM.FieldOperations.Domain.Entities;
using FFM.FieldOperations.Domain.Interfaces;
using Moq;
using Xunit;

namespace FFM.FieldOperations.Tests
{
    public class CheckInVisitCommandHandlerTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock = new();
        private readonly Mock<IClientSiteRepository> _siteRepoMock = new();
        private readonly Mock<IVisitRepository> _visitRepoMock = new();
        private readonly Mock<ICurrentUserService> _userServiceMock = new();

        private CheckInVisitCommandHandler CreateHandler()
        {
            return new CheckInVisitCommandHandler(
                _taskRepoMock.Object,
                _siteRepoMock.Object,
                _visitRepoMock.Object,
                _userServiceMock.Object);
        }

        private ClientSite CreateTestSite(int id = 1, decimal lat = 12.9715987m, decimal lon = 77.5945627m, int radius = 150, bool active = true)
        {
            var site = new ClientSite(
                siteCode: "SITE-BLR-01",
                name: "HDFC Central Bank Facility",
                latitude: lat,
                longitude: lon,
                address: "MG Road, Trinity Circle, Bangalore",
                createdByUserId: 1,
                radiusMeters: radius);

            if (!active)
            {
                // Deactivate via reflection or test helper
                typeof(ClientSite).GetProperty("IsActive")?.SetValue(site, false);
            }
            typeof(ClientSite).GetProperty("Id")?.SetValue(site, id);
            return site;
        }

        private TaskItem CreateTestTask(int id = 5, int siteId = 1, int? assignedEmp = 8, string status = "ASSIGNED")
        {
            var task = new TaskItem(
                taskNumber: $"TSK-TEST-{id:D4}",
                title: "ATM POS Sensor Calibration",
                description: "Calibrate optical sensor on cash dispenser.",
                priority: "HIGH",
                clientSiteId: siteId,
                assignedEmployeeId: assignedEmp,
                scheduledStart: DateTime.UtcNow.AddHours(1),
                scheduledEnd: DateTime.UtcNow.AddHours(3),
                createdByUserId: 1);

            typeof(TaskItem).GetProperty("Id")?.SetValue(task, id);

            if (status == "EN_ROUTE")
            {
                task.StartTravel();
            }
            else if (status == "CHECKED_IN")
            {
                task.CheckIn();
            }
            else if (status == "COMPLETED")
            {
                task.CheckIn();
                task.Complete();
            }
            else if (status == "CANCELLED")
            {
                task.Cancel("Cancelled due to client site closure.");
            }

            return task;
        }

        [Fact]
        public async Task Handle_ValidRequest_WithinGeofence_ReturnsSuccess201()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1, 12.9715987m, 77.5945627m, 150);
            var task = CreateTestTask(5, 1, 8, "ASSIGNED");

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);
            _visitRepoMock.Setup(r => r.GetActiveVisitByEmployeeIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((Visit?)null);
            _visitRepoMock.Setup(r => r.AddVisitAsync(It.IsAny<Visit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Visit v, CancellationToken _) => v);

            _userServiceMock.Setup(u => u.GetUserId()).Returns(8);
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.9716100m,
                Longitude = 77.5945700m,
                AccuracyMeters = 5.0m,
                IsMockLocation = false
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.TaskId);
            Assert.Equal(8, result.EmployeeId);
            Assert.Equal("CHECKED_IN", result.Status);
            Assert.True(result.IsWithinGeofence);
            Assert.False(result.IsIdempotentReplay);
            Assert.True(result.DistanceMeters <= 150m);
            _taskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_OutsideGeofence_ThrowsInvalidOperationException()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1, 12.9715987m, 77.5945627m, 150);
            var task = CreateTestTask(5, 1, 8, "ASSIGNED");

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);
            _visitRepoMock.Setup(r => r.GetActiveVisitByEmployeeIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((Visit?)null);

            _userServiceMock.Setup(u => u.GetUserId()).Returns(8);
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            // Coordinates ~1.5km away
            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.9850000m,
                Longitude = 77.6050000m,
                AccuracyMeters = 10.0m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("meters away from the client site", ex.Message);
        }

        [Fact]
        public async Task Handle_BOLA_EmployeeCannotCheckInToOtherEmployeeTask_ThrowsUnauthorized()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1, 12.9715987m, 77.5945627m, 150);
            var task = CreateTestTask(5, 1, 8, "ASSIGNED"); // Assigned to Employee 8

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);

            _userServiceMock.Setup(u => u.GetUserId()).Returns(12); // Caller is Employee 12
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("does not match assigned technician ID", ex.Message);
        }

        [Fact]
        public async Task Handle_ManagementUser_CanCheckInOnBehalfOfTechnician()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1, 12.9715987m, 77.5945627m, 150);
            var task = CreateTestTask(5, 1, 8, "ASSIGNED");

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);
            _visitRepoMock.Setup(r => r.GetActiveVisitByEmployeeIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((Visit?)null);
            _visitRepoMock.Setup(r => r.AddVisitAsync(It.IsAny<Visit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Visit v, CancellationToken _) => v);

            // Supervisor calling
            _userServiceMock.Setup(u => u.GetUserId()).Returns(2);
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "SUPERVISOR" });

            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(8, result.EmployeeId);
            Assert.Equal("CHECKED_IN", result.Status);
        }

        [Fact]
        public async Task Handle_MockLocation_ThrowsInvalidOperationException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m,
                IsMockLocation = true
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Mock GPS", ex.Message);
        }

        [Fact]
        public async Task Handle_ActiveVisitAlreadyOpen_ThrowsConflictException()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1, 12.9715987m, 77.5945627m, 150);
            var task = CreateTestTask(5, 1, 8, "ASSIGNED");

            var existingActiveVisit = new Visit(
                taskId: 99,
                employeeId: 8,
                checkInTime: DateTime.UtcNow.AddMinutes(-30),
                checkInLatitude: 12.9715987m,
                checkInLongitude: 77.5945627m,
                checkInDistanceMeters: 10m,
                isWithinGeofence: true);
            typeof(Visit).GetProperty("Id")?.SetValue(existingActiveVisit, 489);

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);
            _visitRepoMock.Setup(r => r.GetActiveVisitByEmployeeIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(existingActiveVisit);

            _userServiceMock.Setup(u => u.GetUserId()).Returns(8);
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.9715987m,
                Longitude = 77.5945627m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("active checked-in visit", ex.Message);
        }

        [Fact]
        public async Task Handle_IdempotentRetry_ReturnsExistingVisitWith200()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1, 12.9715987m, 77.5945627m, 150);
            var task = CreateTestTask(5, 1, 8, "CHECKED_IN"); // Task already CHECKED_IN

            var existingVisit = new Visit(
                taskId: 5,
                employeeId: 8,
                checkInTime: DateTime.UtcNow.AddMinutes(-15),
                checkInLatitude: 12.97161m,
                checkInLongitude: 77.59457m,
                checkInDistanceMeters: 14.25m,
                isWithinGeofence: true);
            typeof(Visit).GetProperty("Id")?.SetValue(existingVisit, 501);

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);
            _visitRepoMock.Setup(r => r.GetByTaskIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(existingVisit);

            _userServiceMock.Setup(u => u.GetUserId()).Returns(8);
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.97161m,
                Longitude = 77.59457m
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(501, result.VisitId);
            Assert.True(result.IsIdempotentReplay);
            Assert.Equal("CHECKED_IN", result.Status);
        }

        [Fact]
        public async Task Handle_TaskNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            var handler = CreateHandler();
            _taskRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((TaskItem?)null);

            var command = new CheckInVisitCommand
            {
                TaskId = 999,
                Latitude = 12.97161m,
                Longitude = 77.59457m
            };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_CompletedTask_ThrowsInvalidOperationException()
        {
            // Arrange
            var handler = CreateHandler();
            var site = CreateTestSite(1);
            var task = CreateTestTask(5, 1, 8, "COMPLETED");

            _taskRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);
            _siteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(site);

            _userServiceMock.Setup(u => u.GetUserId()).Returns(8);
            _userServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });

            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 12.97161m,
                Longitude = 77.59457m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("COMPLETED", ex.Message);
        }

        [Fact]
        public async Task Handle_InvalidCoordinates_ThrowsArgumentException()
        {
            // Arrange
            var handler = CreateHandler();
            var command = new CheckInVisitCommand
            {
                TaskId = 5,
                Latitude = 95.0m, // Latitude > 90
                Longitude = 77.59457m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Equal("Latitude", ex.ParamName);
        }
    }
}
