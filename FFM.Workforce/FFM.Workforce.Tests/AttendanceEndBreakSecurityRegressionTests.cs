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
    public class AttendanceEndBreakSecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly EndBreakCommandHandler _handler;

        public AttendanceEndBreakSecurityRegressionTests()
        {
            _handler = new EndBreakCommandHandler(_employeeRepoMock.Object, _attendanceRepoMock.Object);
        }

        [Fact]
        public async Task Security_DeactivatedEmployee_EndBreak_Rejected()
        {
            // Security Scenario: Deactivated employee profile attempts break end (Authorization Bypass / State Desynchronization)
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

            var command = new EndBreakCommand { ActorUserId = 102 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }

        [Fact]
        public async Task Security_EndBreak_WithoutActiveBreak_Rejected()
        {
            // Security Scenario: Attempt to end break when status is PRESENT (State Desynchronization / Data Corruption)
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

            var activeAttendance = new AttendanceRecord(
                501,
                101,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.AddHours(-3),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Shift start",
                null,
                DateTime.UtcNow.AddHours(-3)); // PRESENT status, not ON_BREAK

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101))
                .ReturnsAsync(activeEmp);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(101, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var command = new EndBreakCommand { ActorUserId = 101, Notes = "Illegal end break" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Employee is not currently on an active break", ex.Message);
        }

        [Fact]
        public void Security_Validation_NotesExceeding500Chars_Rejected()
        {
            // Security Scenario: Oversized string injection in notes parameter (Parameter Tampering / Payload Overflow)
            var validator = new EndBreakCommandValidator();
            var oversizedNotes = new string('A', 501);

            var command = new EndBreakCommand
            {
                ActorUserId = 101,
                Notes = oversizedNotes
            };

            var result = validator.Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Notes" && e.ErrorMessage.Contains("500 characters"));
        }
    }
}
