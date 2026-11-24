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
    public class AttendanceCorrectionSecurityRegressionTests
    {
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly CorrectAttendanceCommandHandler _handler;
        private readonly CorrectAttendanceCommandValidator _validator = new();

        public AttendanceCorrectionSecurityRegressionTests()
        {
            _handler = new CorrectAttendanceCommandHandler(
                _attendanceRepoMock.Object,
                _currentUserMock.Object);
        }

        [Fact]
        public async Task Security_EmployeeRole_CorrectionAttempt_Forbidden()
        {
            // Security Scenario: Field employee (EMPLOYEE role) attempts to perform attendance correction (Privilege Escalation)
            _currentUserMock.Setup(c => c.GetRoles())
                .Returns(new[] { "EMPLOYEE" });

            var command = new CorrectAttendanceCommand(
                501,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(8),
                "Self regularization attempt",
                101);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(command, CancellationToken.None));

            Assert.Contains("Insufficient permissions", ex.Message);
        }

        [Fact]
        public void Security_MissingReason_CorrectionAttempt_Rejected()
        {
            // Security Scenario: Mandatory reason check prevention against unverified audit log entries
            var command = new CorrectAttendanceCommand(
                501,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(8),
                "", // Empty reason
                202);

            var validationResult = _validator.Validate(command);

            Assert.False(validationResult.IsValid);
            Assert.Contains(validationResult.Errors, e => e.PropertyName == "Reason" && e.ErrorMessage.Contains("Correction reason is required"));
        }

        [Fact]
        public void Security_CheckOutBeforeCheckIn_Rejected()
        {
            // Security Scenario: Invalid timestamps (check-out earlier than check-in) prevented against negative hours exploit
            var checkIn = new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc);
            var checkOut = new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

            var command = new CorrectAttendanceCommand(
                501,
                checkIn,
                checkOut,
                "Incorrect timestamp entry",
                202);

            var validationResult = _validator.Validate(command);

            Assert.False(validationResult.IsValid);
            Assert.Contains(validationResult.Errors, e => e.ErrorMessage.Contains("Check-out time must be after or equal to check-in time"));
        }
    }
}
