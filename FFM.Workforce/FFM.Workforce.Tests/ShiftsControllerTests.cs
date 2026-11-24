using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Workforce.Api.Controllers;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Handlers;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.Workforce.Tests;

public class ShiftsControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly ShiftsController _controller;

    public ShiftsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new ShiftsController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetAll_WhenCalled_ReturnsOkWithShiftsList()
    {
        // Arrange
        var mockShifts = new List<ShiftDto>
        {
            new ShiftDto { Id = 1, ShiftCode = "MCT-MORN", Name = "Morning Field Shift", DurationHours = 8.0, IsActive = true },
            new ShiftDto { Id = 2, ShiftCode = "MCT-EVEN", Name = "Evening Field Shift", DurationHours = 8.0, IsActive = true }
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllShiftsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockShifts);

        // Act
        var result = await _controller.GetAll(null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<List<ShiftDto>>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(2, apiResponse.Data?.Count);
        Assert.Equal("MCT-MORN", apiResponse.Data?[0].ShiftCode);
    }

    [Fact]
    public async Task GetAll_WhenCustomFilterPassed_PropagatesToMediator()
    {
        // Arrange
        var query = new GetAllShiftsQuery { IsActive = false, IncludeRestDays = false };
        _mediatorMock
            .Setup(m => m.Send(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShiftDto>());

        // Act
        var result = await _controller.GetAll(query, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        _mediatorMock.Verify(m => m.Send(query, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetAllShiftsQueryHandlerTests
{
    private readonly Mock<IShiftRepository> _shiftRepositoryMock;
    private readonly GetAllShiftsQueryHandler _handler;

    public GetAllShiftsQueryHandlerTests()
    {
        _shiftRepositoryMock = new Mock<IShiftRepository>();
        _handler = new GetAllShiftsQueryHandler(_shiftRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectlyMappedShiftDtos()
    {
        // Arrange
        var domainShifts = new List<Shift>
        {
            new Shift(1, "MCT-MORN", "Morning Shift", "Daytime work", new TimeSpan(7, 0, 0), new TimeSpan(15, 0, 0), 15, false, false, "#1C585C"),
            new Shift(2, "MCT-NGHT", "Night Shift", "Emergency work", new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0), 15, true, false, "#4338CA"),
            new Shift(3, "MCT-OFF", "Rest Day", "Weekly off", TimeSpan.Zero, TimeSpan.Zero, 0, false, true, "#64748B")
        };

        _shiftRepositoryMock
            .Setup(r => r.GetAllShiftsAsync(It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainShifts);

        var query = new GetAllShiftsQuery { IsActive = true, IncludeRestDays = true };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // Morning Shift check
        var morning = result[0];
        Assert.Equal("MCT-MORN", morning.ShiftCode);
        Assert.Equal("07:00:00", morning.StartTime);
        Assert.Equal("15:00:00", morning.EndTime);
        Assert.Equal(8.0, morning.DurationHours);
        Assert.False(morning.IsNightShift);

        // Night Shift overnight calculation check (23:00 to 07:00 = 8 hours)
        var night = result[1];
        Assert.Equal("MCT-NGHT", night.ShiftCode);
        Assert.Equal(8.0, night.DurationHours);
        Assert.True(night.IsNightShift);

        // Rest day duration check
        var off = result[2];
        Assert.Equal(0.0, off.DurationHours);
        Assert.True(off.IsRestDay);
    }

    [Fact]
    public void Shift_CalculateDurationHours_HandlesOvernightCorrectly()
    {
        // 23:00 to 07:00 should be exactly 8 hours
        var nightShift = new Shift(3, "NIGHT", "Night", null, new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0), 15, true, false, "#000");
        Assert.Equal(8.0, nightShift.CalculateDurationHours());

        // 08:00 to 17:00 should be 9 hours
        var dayShift = new Shift(4, "GENERAL", "General", null, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 15, false, false, "#000");
        Assert.Equal(9.0, dayShift.CalculateDurationHours());

        // Rest Day should be 0.0 hours
        var restDay = new Shift(5, "OFF", "Off", null, TimeSpan.Zero, TimeSpan.Zero, 0, false, true, "#000");
        Assert.Equal(0.0, restDay.CalculateDurationHours());
    }
}
