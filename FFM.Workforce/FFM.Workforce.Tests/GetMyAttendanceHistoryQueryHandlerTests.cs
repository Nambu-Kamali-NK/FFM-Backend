using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class GetMyAttendanceHistoryQueryHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly GetMyAttendanceHistoryQueryHandler _handler;

        public GetMyAttendanceHistoryQueryHandlerTests()
        {
            _handler = new GetMyAttendanceHistoryQueryHandler(_employeeRepoMock.Object, _attendanceRepoMock.Object);
        }

        [Fact]
        public async Task Handle_ValidRequest_ReturnsPaginatedAttendanceRecords()
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

            var record1 = new AttendanceRecord(
                501,
                10,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.AddHours(-8),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Checkin note",
                null,
                DateTime.UtcNow.AddHours(-8));

            record1.StartBreak("LUNCH", DateTime.UtcNow.AddHours(-4));
            record1.EndBreak(DateTime.UtcNow.AddHours(-3));
            record1.CheckOut(DateTime.UtcNow, "Checkout note");

            var records = new List<AttendanceRecord> { record1 };

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(10))
                .ReturnsAsync(employee);

            _attendanceRepoMock.Setup(r => r.GetHistoryByEmployeeIdAsync(
                    10,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    1,
                    20))
                .ReturnsAsync((records, 1));

            var query = new GetMyAttendanceHistoryQuery
            {
                ActorUserId = 10,
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Equal(501, result.Items[0].Id);
            Assert.Equal(10, result.Items[0].EmployeeId);
            Assert.Equal("COMPLETED", result.Items[0].Status);
            Assert.Single(result.Items[0].Breaks);
            Assert.Equal("LUNCH", result.Items[0].Breaks[0].BreakType);
        }

        [Fact]
        public async Task Handle_EmployeeNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(999))
                .ReturnsAsync((Employee?)null);

            var query = new GetMyAttendanceHistoryQuery { ActorUserId = 999 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _handler.Handle(query, CancellationToken.None));

            Assert.Contains("Employee record not found", ex.Message);
        }
    }
}
