using System;
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
    public class StartBreakCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly StartBreakCommandHandler _handler;

        public StartBreakCommandHandlerTests()
        {
            _handler = new StartBreakCommandHandler(_employeeRepoMock.Object, _attendanceRepoMock.Object);
        }

        [Fact]
        public async Task Handle_ValidActiveCheckIn_StartsBreakSuccessfully()
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

            var activeAttendance = new AttendanceRecord(
                501,
                10,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.AddHours(-2),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Shift start",
                null,
                DateTime.UtcNow.AddHours(-2));

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var command = new StartBreakCommand
            {
                ActorUserId = 10,
                BreakType = "LUNCH"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.EmployeeId);
            Assert.Equal("ON_BREAK", result.Status);
            Assert.Equal("LUNCH", result.BreakType);
            Assert.Equal("Break started successfully.", result.Message);
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

            var command = new StartBreakCommand { ActorUserId = 10, BreakType = "LUNCH" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Employee must be checked in to start a break", ex.Message);
        }

        [Fact]
        public async Task Handle_BreakAlreadyOpen_ThrowsInvalidOperationException()
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

            var activeAttendance = new AttendanceRecord(
                501,
                10,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.AddHours(-2),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Shift start",
                null,
                DateTime.UtcNow.AddHours(-2));
            activeAttendance.StartBreak("LUNCH", DateTime.UtcNow.AddHours(-1)); // Open break

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var command = new StartBreakCommand { ActorUserId = 10, BreakType = "REST" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Cannot start a new break while a break is already open", ex.Message);
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

            var command = new StartBreakCommand { ActorUserId = 10, BreakType = "LUNCH" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }
    }
}
