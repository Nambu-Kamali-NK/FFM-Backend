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
    public class EndBreakCommandHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly EndBreakCommandHandler _handler;

        public EndBreakCommandHandlerTests()
        {
            _handler = new EndBreakCommandHandler(_employeeRepoMock.Object, _attendanceRepoMock.Object);
        }

        [Fact]
        public async Task Handle_ValidOpenBreak_EndsBreakAndTransitionsToPresent()
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
                DateTime.UtcNow.AddHours(-3),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Shift start",
                null,
                DateTime.UtcNow.AddHours(-3));

            // Start break 30 minutes ago
            activeAttendance.StartBreak("LUNCH", DateTime.UtcNow.AddMinutes(-30));

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var command = new EndBreakCommand
            {
                ActorUserId = 10,
                Notes = "Finished lunch"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.EmployeeId);
            Assert.Equal("PRESENT", result.Status);
            Assert.Equal("LUNCH", result.BreakType);
            Assert.True(result.DurationMinutes >= 29);
            Assert.True(result.TotalBreakMinutes >= 29);
            Assert.Equal("Break ended successfully.", result.Message);
            _attendanceRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_NotOnBreak_ThrowsInvalidOperationException()
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
                DateTime.UtcNow.AddHours(-3),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Shift start",
                null,
                DateTime.UtcNow.AddHours(-3)); // PRESENT status, not ON_BREAK

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(10, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var command = new EndBreakCommand { ActorUserId = 10 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Employee is not currently on an active break", ex.Message);
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

            var command = new EndBreakCommand { ActorUserId = 10 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("No active check-in record found to end break", ex.Message);
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

            var command = new EndBreakCommand { ActorUserId = 10 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }
    }
}
