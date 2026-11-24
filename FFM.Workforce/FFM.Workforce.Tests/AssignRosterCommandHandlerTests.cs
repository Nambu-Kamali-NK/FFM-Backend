#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Domain.Entities;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests;

public class AssignRosterCommandHandlerTests
{
    private readonly Mock<IRosterRepository> _rosterRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Mock<IShiftRepository> _shiftRepoMock = new();
    private readonly Mock<ILeaveRepository> _leaveRepoMock = new();
    private readonly AssignRosterCommandHandler _handler;

    private readonly Employee _activeEmployee;
    private readonly Shift _morningShift;

    public AssignRosterCommandHandlerTests()
    {
        _handler = new AssignRosterCommandHandler(
            _rosterRepoMock.Object,
            _employeeRepoMock.Object,
            _shiftRepoMock.Object,
            _leaveRepoMock.Object);

        _activeEmployee = new Employee(
            10,
            "EMP010",
            10,
            "Nambu Kamali",
            1,
            Guid.NewGuid(),
            null,
            "nambu@ffm.gov",
            "9876543210",
            "SYSTEM",
            EmployeeStatus.ACTIVE);

        _morningShift = new Shift(
            1,
            "MCT-MORN",
            "Morning Field Shift",
            "07:00 to 15:00",
            new TimeSpan(7, 0, 0),
            new TimeSpan(15, 0, 0),
            15,
            false,
            false,
            "#1C585C",
            true);
    }

    [Fact]
    public async Task Handle_ValidRequest_AssignsRosterSuccessfully()
    {
        // Arrange
        var command = new AssignRosterCommand
        {
            EmployeeId = _activeEmployee.Id,
            ShiftId = _morningShift.Id,
            RosterDate = DateTime.UtcNow.Date.AddDays(2),
            Zone = "Zone-A",
            Notes = "Routine inspection",
            ActorUserId = 1,
            ActorRole = "MANAGER"
        };

        _employeeRepoMock.Setup(r => r.GetByIdAsync(_activeEmployee.Id)).ReturnsAsync(_activeEmployee);
        _shiftRepoMock.Setup(r => r.GetShiftByIdAsync(_morningShift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_morningShift);
        _leaveRepoMock.Setup(r => r.HasOverlappingLeaveAsync(_activeEmployee.Id, command.RosterDate.Date, command.RosterDate.Date, null)).ReturnsAsync(false);
        _rosterRepoMock.Setup(r => r.HasOverlappingRosterAsync(_activeEmployee.Id, command.RosterDate.Date, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_activeEmployee.Id, result.EmployeeId);
        Assert.Equal("Nambu Kamali", result.EmployeeName);
        Assert.Equal("MCT-MORN", result.ShiftCode);
        Assert.Equal("Zone-A", result.Zone);
        Assert.Equal("ASSIGNED", result.Status);
        Assert.Equal("USER_1 (MANAGER)", result.CreatedBy);

        _rosterRepoMock.Verify(r => r.AddAsync(It.Is<RosterAssignment>(ra =>
            ra.EmployeeId == _activeEmployee.Id &&
            ra.ShiftId == _morningShift.Id &&
            ra.Zone == "Zone-A"), It.IsAny<CancellationToken>()), Times.Once);
        _rosterRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentEmployee_ThrowsKeyNotFoundException()
    {
        // Arrange
        var command = new AssignRosterCommand { EmployeeId = 999, ShiftId = 1, RosterDate = DateTime.UtcNow.Date.AddDays(1) };
        _employeeRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Employee?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Employee with Id 999 not found", ex.Message);
    }

    [Fact]
    public async Task Handle_InactiveEmployee_ThrowsInvalidOperationException()
    {
        // Arrange
        var inactiveEmp = new Employee(99, "EMP099", 99, "Inactive Guy", 1, Guid.NewGuid(), null, "in@ffm.gov", "123", "SYSTEM", EmployeeStatus.PENDING_ACTIVATION);

        var command = new AssignRosterCommand { EmployeeId = inactiveEmp.Id, ShiftId = 1, RosterDate = DateTime.UtcNow.Date.AddDays(1) };
        _employeeRepoMock.Setup(r => r.GetByIdAsync(inactiveEmp.Id)).ReturnsAsync(inactiveEmp);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Only active employees can be assigned", ex.Message);
    }

    [Fact]
    public async Task Handle_NonExistentShift_ThrowsKeyNotFoundException()
    {
        // Arrange
        var command = new AssignRosterCommand { EmployeeId = _activeEmployee.Id, ShiftId = 999, RosterDate = DateTime.UtcNow.Date.AddDays(1) };
        _employeeRepoMock.Setup(r => r.GetByIdAsync(_activeEmployee.Id)).ReturnsAsync(_activeEmployee);
        _shiftRepoMock.Setup(r => r.GetShiftByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Shift?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Shift with Id 999 not found", ex.Message);
    }

    [Fact]
    public async Task Handle_InactiveShift_ThrowsInvalidOperationException()
    {
        // Arrange
        var inactiveShift = new Shift(10, "MCT-DEPR", "Deprecated Shift", null, TimeSpan.Zero, TimeSpan.FromHours(8), 15, false, false, "#000", false);
        var command = new AssignRosterCommand { EmployeeId = _activeEmployee.Id, ShiftId = inactiveShift.Id, RosterDate = DateTime.UtcNow.Date.AddDays(1) };

        _employeeRepoMock.Setup(r => r.GetByIdAsync(_activeEmployee.Id)).ReturnsAsync(_activeEmployee);
        _shiftRepoMock.Setup(r => r.GetShiftByIdAsync(inactiveShift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inactiveShift);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Cannot assign an inactive shift template", ex.Message);
    }

    [Fact]
    public async Task Handle_EmployeeOnLeave_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new AssignRosterCommand { EmployeeId = _activeEmployee.Id, ShiftId = _morningShift.Id, RosterDate = DateTime.UtcNow.Date.AddDays(2) };

        _employeeRepoMock.Setup(r => r.GetByIdAsync(_activeEmployee.Id)).ReturnsAsync(_activeEmployee);
        _shiftRepoMock.Setup(r => r.GetShiftByIdAsync(_morningShift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_morningShift);
        _leaveRepoMock.Setup(r => r.HasOverlappingLeaveAsync(_activeEmployee.Id, command.RosterDate.Date, command.RosterDate.Date, null)).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("employee has an approved or pending leave on this date", ex.Message);
    }

    [Fact]
    public async Task Handle_OverlappingRoster_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new AssignRosterCommand { EmployeeId = _activeEmployee.Id, ShiftId = _morningShift.Id, RosterDate = DateTime.UtcNow.Date.AddDays(2) };

        _employeeRepoMock.Setup(r => r.GetByIdAsync(_activeEmployee.Id)).ReturnsAsync(_activeEmployee);
        _shiftRepoMock.Setup(r => r.GetShiftByIdAsync(_morningShift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_morningShift);
        _leaveRepoMock.Setup(r => r.HasOverlappingLeaveAsync(_activeEmployee.Id, command.RosterDate.Date, command.RosterDate.Date, null)).ReturnsAsync(false);
        _rosterRepoMock.Setup(r => r.HasOverlappingRosterAsync(_activeEmployee.Id, command.RosterDate.Date, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("already exists for this employee on the specified date", ex.Message);
    }
}
