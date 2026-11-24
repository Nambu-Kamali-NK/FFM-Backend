using System;
using System.Collections.Generic;
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
    public class AttendanceCheckInSecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<IGeofenceValidationClient> _geofenceClientMock = new();
        private readonly CheckInCommandHandler _handler;

        public AttendanceCheckInSecurityRegressionTests()
        {
            _geofenceClientMock.Setup(g => g.ValidateLocationAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new GeofenceValidationResult { IsValidGeofence = true });

            _handler = new CheckInCommandHandler(
                _employeeRepoMock.Object,
                _attendanceRepoMock.Object,
                _geofenceClientMock.Object);
        }

        [Fact]
        public async Task Security_DeactivatedEmployee_CheckInAttempt_Rejected()
        {
            // Security Scenario: Deactivated employee profile attempts to bypass check-in restrictions
            var deactivatedEmp = new Employee(
                100,
                "EMP100",
                100,
                "Terminated Employee",
                1,
                Guid.NewGuid(),
                null,
                "terminated@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.DEACTIVATED);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100))
                .ReturnsAsync(deactivatedEmp);

            var command = new CheckInCommand { ActorUserId = 100, CheckInNotes = "Unapproved check-in" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }

        [Fact]
        public async Task Security_DuplicateCheckIn_ReplayAttack_Prevented()
        {
            // Security Scenario: Replay attack or double-click submitting check-in twice for same date
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

            var existingRecord = new AttendanceRecord(101, DateTime.UtcNow, "Initial Checkin");

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101))
                .ReturnsAsync(activeEmp);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(101, It.IsAny<DateTime>()))
                .ReturnsAsync(existingRecord);

            var command = new CheckInCommand { ActorUserId = 101, CheckInNotes = "Replay checkin" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("An active check-in session is already in progress", ex.Message);
        }

        [Fact]
        public void Security_Validation_NotesExceeding500Chars_Rejected()
        {
            // Security Scenario: Overflow attack / extremely large payload injection
            var validator = new CheckInCommandValidator();
            var oversizedNotes = new string('A', 501);

            var command = new CheckInCommand
            {
                ActorUserId = 101,
                CheckInNotes = oversizedNotes
            };

            var result = validator.Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "CheckInNotes" && e.ErrorMessage.Contains("500 characters"));
        }
    }
}
