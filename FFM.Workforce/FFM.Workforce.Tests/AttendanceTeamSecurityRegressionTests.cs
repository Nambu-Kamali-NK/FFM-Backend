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
    public class AttendanceTeamSecurityRegressionTests
    {
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly GetTeamAttendanceQueryHandler _handler;

        public AttendanceTeamSecurityRegressionTests()
        {
            _handler = new GetTeamAttendanceQueryHandler(
                _employeeRepoMock.Object,
                _attendanceRepoMock.Object,
                _currentUserMock.Object);
        }

        [Fact]
        public async Task Security_FieldEmployee_AccessTeamAttendance_Forbidden()
        {
            // Security Scenario: Field employee (EMPLOYEE role) attempts to call team attendance (Privilege Escalation)
            _currentUserMock.Setup(c => c.GetRoles())
                .Returns(new[] { "EMPLOYEE" });

            var query = new GetTeamAttendanceQuery { ActorUserId = 101 };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(query, CancellationToken.None));

            Assert.Contains("Field employees do not have permission", ex.Message);
        }

        [Fact]
        public async Task Security_Manager_CrossOrgUnit_Attendance_Prevented()
        {
            // Security Scenario: Manager attempts to query team attendance outside their assigned OrgUnit (Multi-Tenant Cross-Access)
            _currentUserMock.Setup(c => c.GetRoles())
                .Returns(new[] { "MANAGER" });

            var managerOrgUnit = Guid.NewGuid();
            var outsideOrgUnit = Guid.NewGuid();

            var managerEmp = new Employee(
                2,
                "MGR001",
                20,
                "Manager User",
                4,
                managerOrgUnit, // Manager's real OrgUnit
                null,
                "mgr@test.com",
                "1234567890",
                "System Admin",
                EmployeeStatus.ACTIVE);

            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(20))
                .ReturnsAsync(managerEmp);

            _attendanceRepoMock.Setup(r => r.GetTeamAttendanceAsync(
                    null, // Target OrgUnit is forced to null or manager's own!
                    2,    // Direct reports fallback
                    It.IsAny<DateTime?>(),
                    It.IsAny<AttendanceStatus?>(),
                    1,
                    20))
                .ReturnsAsync((new List<(AttendanceRecord Attendance, Employee Employee)>(), 0));

            var query = new GetTeamAttendanceQuery
            {
                ActorUserId = 20,
                OrgUnitId = outsideOrgUnit // Malicious cross-org unit parameter
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Verify repository was called with manager's direct reports ID fallback (not outsideOrgUnit)
            _attendanceRepoMock.Verify(r => r.GetTeamAttendanceAsync(
                outsideOrgUnit,
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<AttendanceStatus?>(),
                1,
                20), Times.Never);
        }

        [Fact]
        public void Security_PageSize_ExceedingLimit_Rejected()
        {
            // Security Scenario: PageSize validation enforcement against DoS
            var validator = new GetTeamAttendanceQueryValidator();
            var query = new GetTeamAttendanceQuery
            {
                ActorUserId = 20,
                PageSize = 500
            };

            var result = validator.Validate(query);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "PageSize" && e.ErrorMessage.Contains("between 1 and 100"));
        }
    }
}
