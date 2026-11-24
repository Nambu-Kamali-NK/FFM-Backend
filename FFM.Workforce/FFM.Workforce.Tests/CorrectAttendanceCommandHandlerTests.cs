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
    public class CorrectAttendanceCommandHandlerTests
    {
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly CorrectAttendanceCommandHandler _handler;

        public CorrectAttendanceCommandHandlerTests()
        {
            _handler = new CorrectAttendanceCommandHandler(
                _attendanceRepoMock.Object,
                _currentUserMock.Object);
        }

        [Fact]
        public async Task Handle_ValidCorrection_UpdatesStatusToCorrected_And_CreatesAuditLog()
        {
            // Arrange
            _currentUserMock.Setup(c => c.GetRoles()).Returns(new[] { "MANAGER" });
            _currentUserMock.Setup(c => c.GetUserId()).Returns(202);

            var checkIn = new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);
            var checkOut = new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc);
            var attendance = new AttendanceRecord(101, checkIn, "Initial checkin");

            _attendanceRepoMock.Setup(r => r.GetByIdAsync(501))
                .ReturnsAsync(attendance);

            var command = new CorrectAttendanceCommand(
                501,
                checkIn,
                checkOut,
                "Employee forgot to check out due to phone crash",
                202);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(501, result.AttendanceId);
            Assert.Equal(202, result.CorrectedByUserId);
            Assert.Equal("CORRECTED", result.Status);
            Assert.Equal(9.0, result.NetWorkingHours);

            _attendanceRepoMock.Verify(r => r.AddAuditLogAsync(It.Is<AttendanceAuditLog>(log =>
                log.AttendanceId == 501 &&
                log.ActorUserId == 202 &&
                log.Action == "CORRECT_ATTENDANCE" &&
                log.Reason == "Employee forgot to check out due to phone crash")), Times.Once);

            _attendanceRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_MissingAttendanceRecord_ThrowsKeyNotFoundException()
        {
            // Arrange
            _currentUserMock.Setup(c => c.GetRoles()).Returns(new[] { "MANAGER" });
            _attendanceRepoMock.Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((AttendanceRecord?)null);

            var command = new CorrectAttendanceCommand(
                999,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(8),
                "Correction reason",
                202);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _handler.Handle(command, CancellationToken.None));
        }
    }
}
