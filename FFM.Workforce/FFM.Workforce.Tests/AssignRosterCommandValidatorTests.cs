using System;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace FFM.Workforce.Tests;

public class AssignRosterCommandValidatorTests
{
    private readonly AssignRosterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoValidationErrors()
    {
        // Arrange
        var command = new AssignRosterCommand
        {
            EmployeeId = 10,
            ShiftId = 1,
            RosterDate = DateTime.UtcNow.Date.AddDays(1),
            Zone = "North-Zone",
            Notes = "Valid roster note"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidEmployeeId_HasValidationError(int invalidId)
    {
        var command = new AssignRosterCommand
        {
            EmployeeId = invalidId,
            ShiftId = 1,
            RosterDate = DateTime.UtcNow.Date.AddDays(1)
        };

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_InvalidShiftId_HasValidationError(int invalidId)
    {
        var command = new AssignRosterCommand
        {
            EmployeeId = 10,
            ShiftId = invalidId,
            RosterDate = DateTime.UtcNow.Date.AddDays(1)
        };

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ShiftId);
    }

    [Fact]
    public void Validate_PastRosterDate_HasValidationError()
    {
        var command = new AssignRosterCommand
        {
            EmployeeId = 10,
            ShiftId = 1,
            RosterDate = DateTime.UtcNow.Date.AddDays(-10)
        };

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RosterDate);
    }

    [Fact]
    public void Validate_TodayRosterDate_HasValidationError()
    {
        var command = new AssignRosterCommand
        {
            EmployeeId = 10,
            ShiftId = 1,
            RosterDate = DateTime.UtcNow.Date
        };

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RosterDate);
    }
}
