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
    public class CheckInCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<IGeofenceValidationClient> _geofenceClientMock = new();
        private readonly CheckInCommandHandler _handler;

        public CheckInCommandHandlerTests()
        {
            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new GeofenceValidationResult { IsValidGeofence = true });

            _handler = new CheckInCommandHandler(
                _employeeRepoMock.Object,
                _attendanceRepoMock.Object,
                _geofenceClientMock.Object);
        }

        [Fact]
        public async Task Handle_ValidActiveEmployee_CreatesCheckInSuccessfully()
        {
            // Arrange
            var employee = new Employee(
                10,
                "EMP010",
                10,
                "Active Employee",
                1,
                Guid.NewGuid(),
                null,
                "emp10@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync((AttendanceRecord?)null);

            var command = new CheckInCommand
            {
                ActorUserId = 10,
                CheckInNotes = "Starting shift"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.EmployeeId);
            Assert.Equal("PRESENT", result.Status);
            Assert.Equal("Starting shift", result.CheckInNotes);
            _attendanceRepoMock.Verify(r => r.AddAsync(It.IsAny<AttendanceRecord>()), Times.Once);
            _attendanceRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_InactiveEmployee_ThrowsInvalidOperationException()
        {
            // Arrange
            var deactivatedEmp = new Employee(
                10,
                "EMP010",
                10,
                "Deactivated Employee",
                1,
                Guid.NewGuid(),
                null,
                "emp10@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.DEACTIVATED);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(deactivatedEmp);

            var command = new CheckInCommand
            {
                ActorUserId = 10,
                CheckInNotes = "Trying to check in"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }

        [Fact]
        public async Task Handle_DuplicateCheckIn_ThrowsInvalidOperationException()
        {
            // Arrange
            var employee = new Employee(
                10,
                "EMP010",
                10,
                "Active Employee",
                1,
                Guid.NewGuid(),
                null,
                "emp10@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            var existingAttendance = new AttendanceRecord(10, DateTime.UtcNow, "Morning checkin");

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(existingAttendance);

            var command = new CheckInCommand
            {
                ActorUserId = 10,
                CheckInNotes = "Second checkin attempt"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("An active check-in session is already in progress", ex.Message);
        }

        [Fact]
        public async Task Handle_CheckedOutEmployee_CanCheckInAgainSameDay()
        {
            // Arrange (Employee checked out of morning session, now checking into afternoon session)
            var employee = new Employee(
                10,
                "EMP010",
                10,
                "Active Employee",
                1,
                Guid.NewGuid(),
                null,
                "emp10@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            // Repository returns null because no UNCOMPLETED active session exists
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync((AttendanceRecord?)null);

            var command = new CheckInCommand
            {
                ActorUserId = 10,
                CheckInNotes = "Afternoon shift check-in"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PRESENT", result.Status);
            Assert.Equal("Afternoon shift check-in", result.CheckInNotes);
            _attendanceRepoMock.Verify(r => r.AddAsync(It.IsAny<AttendanceRecord>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NonExistentUser_ThrowsKeyNotFoundException()
        {
            // Arrange
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(999))
                .ReturnsAsync((Employee?)null);

            var command = new CheckInCommand { ActorUserId = 999 };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_OutsideGeofence_ThrowsInvalidOperationException()
        {
            // Arrange
            var employee = new Employee(
                10,
                "EMP010",
                10,
                "Active Employee",
                1,
                Guid.NewGuid(),
                null,
                "emp10@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync((AttendanceRecord?)null);

            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(10, 73.9696, 21.3045, It.IsAny<double>()))
                .ReturnsAsync(new GeofenceValidationResult
                {
                    IsValidGeofence = false,
                    Message = "User location is outside assigned geofence boundaries."
                });

            var command = new CheckInCommand
            {
                ActorUserId = 10,
                Latitude = 73.9696,
                Longitude = 21.3045,
                CheckInNotes = "Outside check-in attempt"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("outside your assigned work geofence boundary", ex.Message);
        }

        [Fact]
        public async Task Handle_GpsServiceThrowsException_ThrowsGracefulUnavailableException()
        {
            // Arrange: GPS service goes down / times out
            var employee = new Employee(10, "EMP010", 10, "Active Employee", 1, Guid.NewGuid(), null, "emp10@test.com", "1234567890", "System Admin", EmployeeStatus.ACTIVE);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10)).ReturnsAsync(employee);
            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>())).ReturnsAsync((AttendanceRecord?)null);

            // Simulate GPS service failing or timing out with HttpRequestException
            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .ThrowsAsync(new System.Net.Http.HttpRequestException("Connection refused by GPS service (502/503)"));

            var command = new CheckInCommand
            {
                ActorUserId = 10,
                Latitude = 13.0827,
                Longitude = 80.2707,
                CheckInNotes = "Check-in during GPS outage"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("temporarily unavailable", ex.Message);
            Assert.Contains("Please retry in a few moments", ex.Message);
        }
    }
}
