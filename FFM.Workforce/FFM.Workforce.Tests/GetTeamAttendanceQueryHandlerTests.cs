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
    public class GetTeamAttendanceQueryHandlerTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly GetTeamAttendanceQueryHandler _handler;

        public GetTeamAttendanceQueryHandlerTests()
        {
            _handler = new GetTeamAttendanceQueryHandler(
                _employeeRepoMock.Object,
                _attendanceRepoMock.Object,
                _currentUserMock.Object);
        }

        [Fact]
        public async Task Handle_ManagerRole_ReturnsTeamAttendanceRecords()
        {
            // Arrange
            _currentUserMock.Setup(c => c.GetRoles())
                .Returns(new[] { "MANAGER" });

            var managerGuid = Guid.NewGuid();
            var managerEmp = new Employee(
                2,
                "MGR001",
                20,
                "Manager User",
                4,
                managerGuid,
                null,
                "mgr@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            var staffEmp = new Employee(
                10,
                "EMP010",
                10,
                "Staff Member",
                6,
                managerGuid,
                2,
                "staff@test.com",
                "1234567891",
                "System Admin",
                EmployeeStatus.ACTIVE);

            var attendance = new AttendanceRecord(
                501,
                10,
                DateTime.UtcNow.Date,
                DateTime.UtcNow.AddHours(-4),
                null,
                AttendanceStatus.PRESENT,
                0,
                0,
                "Shift start",
                null,
                DateTime.UtcNow.AddHours(-4));

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(20))
                .ReturnsAsync(managerEmp);

            var dbResults = new List<(AttendanceRecord Attendance, Employee Employee)>
            {
                (attendance, staffEmp)
            };

            _attendanceRepoMock.Setup(r => r.GetTeamAttendanceAsync(
                    managerGuid,
                    It.IsAny<int?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<AttendanceStatus?>(),
                    1,
                    20))
                .ReturnsAsync((dbResults, 1));

            var query = new GetTeamAttendanceQuery
            {
                ActorUserId = 20,
                OrgUnitId = managerGuid,
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Equal("EMP010", result.Items[0].EmployeeCode);
            Assert.Equal("Staff Member", result.Items[0].EmployeeName);
            Assert.Equal("PRESENT", result.Items[0].Status);
        }
    }
}
