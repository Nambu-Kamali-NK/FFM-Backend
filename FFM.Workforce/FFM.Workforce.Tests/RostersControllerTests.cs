using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests;

public class RostersControllerTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly RostersController _controller;

    public RostersControllerTests()
    {
        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns(101);
        _currentUserServiceMock.Setup(u => u.GetRoles()).Returns(new[] { "SUPERVISOR" });

        _controller = new RostersController(_mediatorMock.Object, _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task Assign_ValidRequest_ReturnsOkWithRosterAssignment()
    {
        // Arrange
        var request = new AssignRosterRequest
        {
            EmployeeId = 10,
            ShiftId = 1,
            RosterDate = DateTime.UtcNow.Date.AddDays(1),
            Zone = "North-Zone",
            Notes = "Water line valve checking"
        };

        var expectedDto = new RosterAssignmentDto
        {
            Id = 42,
            EmployeeId = 10,
            EmployeeCode = "EMP010",
            EmployeeName = "Nambu Kamali",
            ShiftId = 1,
            ShiftCode = "MCT-MORN",
            ShiftName = "Morning Field Shift",
            StartTime = new TimeSpan(7, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            ColorCode = "#1C585C",
            RosterDate = request.RosterDate,
            Zone = "North-Zone",
            Notes = "Water line valve checking",
            Status = "ASSIGNED",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "USER_101 (SUPERVISOR)"
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AssignRosterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.Assign(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<RosterAssignmentDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(42, apiResponse.Data.Id);
        Assert.Equal("MCT-MORN", apiResponse.Data.ShiftCode);
        Assert.Equal("North-Zone", apiResponse.Data.Zone);
        Assert.Equal("ASSIGNED", apiResponse.Data.Status);
    }

    [Fact]
    public async Task Assign_WhenMediatorFails_PropagatesException()
    {
        // Arrange
        var request = new AssignRosterRequest
        {
            EmployeeId = 999,
            ShiftId = 1,
            RosterDate = DateTime.UtcNow.Date.AddDays(1)
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AssignRosterCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Employee with Id 999 not found."));

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _controller.Assign(request, CancellationToken.None));
    }
}
