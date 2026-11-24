using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Application.Validators;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class AttendanceHistorySecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly GetMyAttendanceHistoryQueryHandler _handler;

        public AttendanceHistorySecurityRegressionTests()
        {
            _handler = new GetMyAttendanceHistoryQueryHandler(_employeeRepoMock.Object, _attendanceRepoMock.Object);
        }

        [Fact]
        public async Task Security_CrossEmployee_DataLeakage_Prevented()
        {
            // Security Scenario: Employee user tries to fetch history; handler MUST use target employee resolved from ActorUserId ONLY
            var targetEmp = new Employee(
                101,
                "EMP101",
                101,
                "Employee 101",
                1,
                Guid.NewGuid(),
                null,
                "emp101@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(101))
                .ReturnsAsync(targetEmp);

            _attendanceRepoMock.Setup(r => r.GetHistoryByEmployeeIdAsync(
                    101, // Must strictly be 101!
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()))
                .ReturnsAsync((new List<AttendanceRecord>(), 0));

            var query = new GetMyAttendanceHistoryQuery { ActorUserId = 101 };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            _attendanceRepoMock.Verify(r => r.GetHistoryByEmployeeIdAsync(
                101,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()), Times.Once);

            // Ensure no call was made with another employee ID (e.g. employee 999 or 202)
            _attendanceRepoMock.Verify(r => r.GetHistoryByEmployeeIdAsync(
                It.Is<int>(id => id != 101),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void Security_InvalidDateRange_Rejected()
        {
            // Security Scenario: Malformed date range filter (StartDate > EndDate)
            var validator = new GetMyAttendanceHistoryQueryValidator();
            var query = new GetMyAttendanceHistoryQuery
            {
                ActorUserId = 101,
                StartDate = new DateTime(2026, 10, 15),
                EndDate = new DateTime(2026, 10, 01)
            };

            var result = validator.Validate(query);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("StartDate cannot be later than EndDate"));
        }

        [Fact]
        public void Security_PageSize_ExceedingLimit_Rejected()
        {
            // Security Scenario: Denial of Service / Payload bomb via PageSize=1000
            var validator = new GetMyAttendanceHistoryQueryValidator();
            var query = new GetMyAttendanceHistoryQuery
            {
                ActorUserId = 101,
                PageSize = 1000
            };

            var result = validator.Validate(query);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "PageSize" && e.ErrorMessage.Contains("between 1 and 100"));
        }
    }
}
