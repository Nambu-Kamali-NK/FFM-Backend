using System;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Validators;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class AttendanceCheckOutSecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<IGeofenceValidationClient> _geofenceClientMock = new();
        private readonly CheckOutCommandHandler _handler;

        public AttendanceCheckOutSecurityRegressionTests()
        {
            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new GeofenceValidationResult { IsValidGeofence = true });

            _handler = new CheckOutCommandHandler(
                _employeeRepoMock.Object,
                _attendanceRepoMock.Object,
                _geofenceClientMock.Object);
        }

        [Fact]
        public async Task Security_MissingActiveCheckIn_CheckOutAttempt_Rejected()
        {
            // Security Scenario: User attempts to trigger checkout without having an active check-in record
            var activeEmp = new Employee(
                101,
                "EMP101",
                101,
                "Active Employee",
                1,
                Guid.NewGuid(),
                null,
                "active@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101))
                .ReturnsAsync(activeEmp);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(101, It.IsAny<DateTime>()))
                .ReturnsAsync((AttendanceRecord?)null);

            var command = new CheckOutCommand { ActorUserId = 101, CheckOutNotes = "Bypass checkout" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("No active check-in record found for checkout", ex.Message);
        }

        [Fact]
        public async Task Security_DeactivatedEmployee_CheckOutAttempt_Rejected()
        {
            // Security Scenario: Terminated/Suspended employee attempts to invoke check-out endpoint
            var deactivatedEmp = new Employee(
                102,
                "EMP102",
                102,
                "Deactivated Employee",
                1,
                Guid.NewGuid(),
                null,
                "deact@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.DEACTIVATED);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(102))
                .ReturnsAsync(deactivatedEmp);

            var command = new CheckOutCommand { ActorUserId = 102, CheckOutNotes = "Unauthorized checkout" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }

        [Fact]
        public void Security_Validation_NotesExceeding500Chars_Rejected()
        {
            // Security Scenario: Oversized string injection in CheckOutNotes
            var validator = new CheckOutCommandValidator();
            var oversizedNotes = new string('X', 501);

            var command = new CheckOutCommand
            {
                ActorUserId = 101,
                CheckOutNotes = oversizedNotes
            };

            var result = validator.Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "CheckOutNotes" && e.ErrorMessage.Contains("500 characters"));
        }
    }
}
