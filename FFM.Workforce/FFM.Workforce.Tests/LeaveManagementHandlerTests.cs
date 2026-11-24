using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class LeaveManagementHandlerTests
    {
        private readonly Mock<ILeaveRepository> _leaveRepoMock = new();
        private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();

        private Employee CreateTestEmployee(int id, int userId, EmployeeStatus status, Guid orgUnitId, int? managerId = null)
        {
            return new Employee(
                id,
                $"EMP{id:D3}",
                userId,
                $"Employee {id}",
                1,
                orgUnitId,
                managerId,
                $"emp{id}@test.com",
                "9876543210",
                "SYSTEM",
                status);
        }

        [Fact]
        public async Task GetLeaveTypes_ReturnsConfiguredActiveLeaveTypes()
        {
            // Arrange
            var types = new List<LeaveType>
            {
                new LeaveType(1, "CASUAL", "Casual Leave", 12m, true, false, true),
                new LeaveType(2, "SICK", "Sick Leave", 10m, true, false, true)
            };
            _leaveRepoMock.Setup(r => r.GetLeaveTypesAsync(true)).ReturnsAsync(types);

            var handler = new GetLeaveTypesQueryHandler(_leaveRepoMock.Object);

            // Act
            var result = await handler.Handle(new GetLeaveTypesQuery(), CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("CASUAL", result[0].Code);
            Assert.Equal(12m, result[0].DefaultDaysPerYear);
        }

        [Fact]
        public async Task GetLeaveBalances_CallerEmployee_ReturnsBalances()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var emp = CreateTestEmployee(10, 100, EmployeeStatus.ACTIVE, orgUnitId);

            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100)).ReturnsAsync(emp);

            var casualType = new LeaveType(1, "CASUAL", "Casual Leave", 12m, true, false, true);
            var balance = new LeaveBalance(10, 1, 2026, 12m, 2m);
            balance.GetType().GetProperty(nameof(LeaveBalance.LeaveType))?.SetValue(balance, casualType);

            _leaveRepoMock.Setup(r => r.GetLeaveBalancesAsync(10, 2026)).ReturnsAsync(new List<LeaveBalance> { balance });

            var handler = new GetLeaveBalancesQueryHandler(_leaveRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);

            // Act
            var result = await handler.Handle(new GetLeaveBalancesQuery { ActorUserId = 100, Year = 2026 }, CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal("CASUAL", result[0].LeaveTypeCode);
            Assert.Equal(10m, result[0].AvailableDays);
        }

        [Fact]
        public async Task SubmitLeaveRequest_InactiveEmployee_ThrowsInvalidOperationException()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var emp = CreateTestEmployee(10, 100, EmployeeStatus.DEACTIVATED, orgUnitId);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100)).ReturnsAsync(emp);

            var handler = new SubmitLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object);

            var command = new SubmitLeaveRequestCommand
            {
                ActorUserId = 100,
                LeaveTypeId = 1,
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                Reason = "Vacation"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Only active employees", ex.Message);
        }

        [Fact]
        public async Task SubmitLeaveRequest_OverlappingDates_ThrowsInvalidOperationException()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var emp = CreateTestEmployee(10, 100, EmployeeStatus.ACTIVE, orgUnitId);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100)).ReturnsAsync(emp);

            var start = DateTime.UtcNow.AddDays(5);
            var end = DateTime.UtcNow.AddDays(6);
            _leaveRepoMock.Setup(r => r.HasOverlappingLeaveAsync(10, start, end, null)).ReturnsAsync(true);

            var handler = new SubmitLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object);

            var command = new SubmitLeaveRequestCommand
            {
                ActorUserId = 100,
                LeaveTypeId = 1,
                StartDate = start,
                EndDate = end,
                Reason = "Vacation"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public async Task SubmitLeaveRequest_InsufficientBalance_ThrowsInvalidOperationException()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var emp = CreateTestEmployee(10, 100, EmployeeStatus.ACTIVE, orgUnitId);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100)).ReturnsAsync(emp);

            var start = DateTime.UtcNow.AddDays(5);
            var end = DateTime.UtcNow.AddDays(9); // 5 days
            _leaveRepoMock.Setup(r => r.HasOverlappingLeaveAsync(10, start, end, null)).ReturnsAsync(false);

            var casualType = new LeaveType(1, "CASUAL", "Casual Leave", 12m, true, false, true);
            _leaveRepoMock.Setup(r => r.GetLeaveTypeByIdAsync(1)).ReturnsAsync(casualType);

            // Available is only 2 days (12 - 10 = 2)
            var balance = new LeaveBalance(10, 1, start.Year, 12m, 10m);
            balance.GetType().GetProperty(nameof(LeaveBalance.LeaveType))?.SetValue(balance, casualType);
            _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync(10, 1, start.Year)).ReturnsAsync(balance);

            var handler = new SubmitLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object);

            var command = new SubmitLeaveRequestCommand
            {
                ActorUserId = 100,
                LeaveTypeId = 1,
                StartDate = start,
                EndDate = end,
                Reason = "Long Vacation"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("exceeds available balance", ex.Message);
        }

        [Fact]
        public async Task SubmitLeaveRequest_ValidRequest_PersistsAndReturnsPending()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var emp = CreateTestEmployee(10, 100, EmployeeStatus.ACTIVE, orgUnitId);
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100)).ReturnsAsync(emp);

            var start = DateTime.UtcNow.AddDays(5);
            var end = DateTime.UtcNow.AddDays(6); // 2 days
            _leaveRepoMock.Setup(r => r.HasOverlappingLeaveAsync(10, start, end, null)).ReturnsAsync(false);

            var casualType = new LeaveType(1, "CASUAL", "Casual Leave", 12m, true, false, true);
            _leaveRepoMock.Setup(r => r.GetLeaveTypeByIdAsync(1)).ReturnsAsync(casualType);

            var balance = new LeaveBalance(10, 1, start.Year, 12m, 0m);
            balance.GetType().GetProperty(nameof(LeaveBalance.LeaveType))?.SetValue(balance, casualType);
            _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync(10, 1, start.Year)).ReturnsAsync(balance);

            var handler = new SubmitLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object);

            var command = new SubmitLeaveRequestCommand
            {
                ActorUserId = 100,
                LeaveTypeId = 1,
                StartDate = start,
                EndDate = end,
                Reason = "Family trip"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("PENDING", result.Status);
            Assert.Equal(2.0m, result.DaysCount);
            _leaveRepoMock.Verify(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>()), Times.Once);
            _leaveRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DecideLeaveRequest_SelfApproval_ThrowsInvalidOperationException()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var manager = CreateTestEmployee(5, 50, EmployeeStatus.ACTIVE, orgUnitId);

            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(manager);

            var leaveRequest = new LeaveRequest(5, 1, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(3), 2m, false, "Leave");
            _leaveRepoMock.Setup(r => r.GetLeaveRequestByIdAsync(1)).ReturnsAsync(leaveRequest);

            var handler = new DecideLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);

            var command = new DecideLeaveRequestCommand
            {
                RequestId = 1,
                ActorUserId = 50,
                Decision = "APPROVED"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Contains("Self-approval is strictly forbidden", ex.Message);
        }

        [Fact]
        public async Task DecideLeaveRequest_ApprovedByManager_DeductsBalance()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var manager = CreateTestEmployee(5, 50, EmployeeStatus.ACTIVE, orgUnitId);
            var subordinate = CreateTestEmployee(10, 100, EmployeeStatus.ACTIVE, orgUnitId, managerId: 5);

            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "MANAGER" });
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(50)).ReturnsAsync(manager);
            _employeeRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(subordinate);

            var casualType = new LeaveType(1, "CASUAL", "Casual Leave", 12m, true, false, true);
            var leaveRequest = new LeaveRequest(10, 1, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(3), 2m, false, "Subordinate Leave");
            leaveRequest.GetType().GetProperty(nameof(LeaveRequest.LeaveType))?.SetValue(leaveRequest, casualType);
            _leaveRepoMock.Setup(r => r.GetLeaveRequestByIdAsync(101)).ReturnsAsync(leaveRequest);

            var balance = new LeaveBalance(10, 1, DateTime.UtcNow.Year, 12m, 0m);
            balance.GetType().GetProperty(nameof(LeaveBalance.LeaveType))?.SetValue(balance, casualType);
            _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync(10, 1, It.IsAny<int>())).ReturnsAsync(balance);

            var handler = new DecideLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);

            var command = new DecideLeaveRequestCommand
            {
                RequestId = 101,
                ActorUserId = 50,
                Decision = "APPROVED",
                Comments = "Approved, have a good break"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("APPROVED", result.Status);
            Assert.Equal(2m, balance.UsedDays);
            Assert.Equal(10m, balance.AvailableDays);
            _leaveRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CancelLeaveRequest_ApprovedFutureLeave_RefundsBalance()
        {
            // Arrange
            var orgUnitId = Guid.NewGuid();
            var emp = CreateTestEmployee(10, 100, EmployeeStatus.ACTIVE, orgUnitId);

            _currentUserMock.Setup(u => u.GetRoles()).Returns(new[] { "EMPLOYEE" });
            _employeeRepoMock.Setup(r => r.GetByUserIdAsync(100)).ReturnsAsync(emp);

            var casualType = new LeaveType(1, "CASUAL", "Casual Leave", 12m, true, false, true);
            var leaveRequest = new LeaveRequest(10, 1, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6), 2m, false, "Future trip");
            leaveRequest.GetType().GetProperty(nameof(LeaveRequest.LeaveType))?.SetValue(leaveRequest, casualType);
            leaveRequest.Approve(5, "Approved previously");

            _leaveRepoMock.Setup(r => r.GetLeaveRequestByIdAsync(202)).ReturnsAsync(leaveRequest);

            // Balance currently has UsedDays = 2
            var balance = new LeaveBalance(10, 1, DateTime.UtcNow.Year, 12m, 2m);
            _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync(10, 1, It.IsAny<int>())).ReturnsAsync(balance);

            var handler = new CancelLeaveRequestCommandHandler(_leaveRepoMock.Object, _employeeRepoMock.Object, _currentUserMock.Object);

            var command = new CancelLeaveRequestCommand
            {
                RequestId = 202,
                ActorUserId = 100
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal("CANCELLED", result.Status);
            Assert.Equal(0m, balance.UsedDays); // Refunded!
            Assert.Equal(12m, balance.AvailableDays);
            _leaveRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }
    }
}
