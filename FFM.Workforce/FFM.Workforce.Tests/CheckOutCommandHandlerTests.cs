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
    public class CheckOutCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<IGeofenceValidationClient> _geofenceClientMock = new();
        private readonly CheckOutCommandHandler _handler;

        public CheckOutCommandHandlerTests()
        {
            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new GeofenceValidationResult { IsValidGeofence = true });

            _handler = new CheckOutCommandHandler(
                _employeeRepoMock.Object,
                _attendanceRepoMock.Object,
                _geofenceClientMock.Object);
        }

        [Fact]
        public async Task Handle_ValidActiveCheckIn_CalculatesHoursAndChecksOutSuccessfully()
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

            // CheckIn 8 hours ago
            var checkInTime = DateTime.UtcNow.AddHours(-8);
            var activeAttendance = new AttendanceRecord(10, checkInTime, "Shift start");

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var command = new CheckOutCommand
            {
                ActorUserId = 10,
                CheckOutNotes = "Finished 5 visits"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.EmployeeId);
            Assert.Equal("COMPLETED", result.Status);
            Assert.True(result.NetWorkingHours >= 7.9);
            Assert.Equal("Finished 5 visits", result.CheckOutNotes);
            _attendanceRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_NoActiveCheckIn_ThrowsInvalidOperationException()
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

            var command = new CheckOutCommand { ActorUserId = 10 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("No active check-in record found for checkout", ex.Message);
        }

        [Fact]
        public async Task Handle_AlreadyCheckedOut_ThrowsInvalidOperationException()
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

            var checkInTime = DateTime.UtcNow.AddHours(-8);
            var completedAttendance = new AttendanceRecord(10, checkInTime, "Shift start");
            completedAttendance.CheckOut(DateTime.UtcNow, "Already checked out");

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(completedAttendance);

            var command = new CheckOutCommand { ActorUserId = 10 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("already checked out", ex.Message);
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

            var command = new CheckOutCommand { ActorUserId = 10 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
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

            var activeAttendance = new AttendanceRecord(10, DateTime.UtcNow.AddHours(-4), "Started shift");

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(10, 75.6869, 89.9090, It.IsAny<double>()))
                .ReturnsAsync(new GeofenceValidationResult
                {
                    IsValidGeofence = false,
                    Message = "User location is outside all assigned geofence boundaries."
                });

            var command = new CheckOutCommand
            {
                ActorUserId = 10,
                Latitude = 75.6869,
                Longitude = 89.9090,
                CheckOutNotes = "Trying to check out from outside site"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("outside your assigned work geofence boundary", ex.Message);
        }
    }
}
