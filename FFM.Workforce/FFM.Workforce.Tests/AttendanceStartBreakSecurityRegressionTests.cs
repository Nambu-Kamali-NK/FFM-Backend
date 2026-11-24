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
    public class AttendanceStartBreakSecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly StartBreakCommandHandler _handler;

        public AttendanceStartBreakSecurityRegressionTests()
        {
            _handler = new StartBreakCommandHandler(_employeeRepoMock.Object, _attendanceRepoMock.Object);
        }

        [Fact]
        public async Task Security_DeactivatedEmployee_StartBreak_Rejected()
        {
            // Security Scenario: Deactivated employee profile attempts break start (Authorization Bypass / State Desynchronization)
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

            var command = new StartBreakCommand { ActorUserId = 102, BreakType = "LUNCH" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Deactivated or inactive employees cannot record attendance", ex.Message);
        }

        [Fact]
        public async Task Security_MultipleOpenBreaks_ReplayAttack_Prevented()
        {
            // Security Scenario: Replay attack or double submission starting 2 breaks (Double Break Replay / Data Corruption)
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
                DateTime.UtcNow.AddHours(-3));
            activeAttendance.StartBreak("LUNCH", DateTime.UtcNow.AddHours(-1)); // First break is open

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101))
                .ReturnsAsync(activeEmp);

            _attendanceRepoMock.Setup(r => r.GetActiveAttendanceByEmployeeIdAsync(101, It.IsAny<DateTime>()))
                .ReturnsAsync(activeAttendance);

            var secondBreakCommand = new StartBreakCommand { ActorUserId = 101, BreakType = "REST" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(secondBreakCommand, CancellationToken.None));

            Assert.Contains("Cannot start a new break while a break is already open", ex.Message);
        }

        [Theory]
        [InlineData("INVALID_TYPE")]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("LUNCH'; DROP TABLE BreakRecords;--")]
        public void Security_InvalidBreakType_Injection_Rejected(string maliciousBreakType)
        {
            // Security Scenario: SQL/Payload string injection in breakType (Parameter Tampering / Payload Injection)
            var validator = new StartBreakCommandValidator();
            var command = new StartBreakCommand
            {
                ActorUserId = 101,
                BreakType = maliciousBreakType
            };

            var result = validator.Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "BreakType" && e.ErrorMessage.Contains("BreakType must be 'LUNCH', 'REST', or 'PERSONAL'"));
        }
    }
}
