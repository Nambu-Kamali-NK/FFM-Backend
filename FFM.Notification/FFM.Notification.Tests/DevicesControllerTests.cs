using FFM.Notification.Api.Controllers;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class DevicesControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly DevicesController _controller;

    public DevicesControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockUserService = new Mock<ICurrentUserService>();
        _controller = new DevicesController(_mockMediator.Object, _mockUserService.Object);
    }

    [Fact]
    public async Task RegisterDevice_ValidRequest_ReturnsOkWithData()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetUserId()).Returns(42);

        var request = new RegisterDeviceRequest
        {
            DeviceId = "device-test-123",
            Platform = "ANDROID",
            DeviceModel = "OnePlus 11",
            OsVersion = "Android 14",
            AppVersion = "2.0.0",
            FcmToken = new string('x', 100)
        };

        var expectedResponse = new DeviceRegistrationResponse
        {
            DeviceRegistrationId = 1,
            EmployeeId = 42,
            DeviceId = "device-test-123",
            Platform = "ANDROID",
            DeviceModel = "OnePlus 11",
            IsActive = true
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<RegisterDeviceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.RegisterDevice(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<DeviceRegistrationResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(42, apiResponse.Data?.EmployeeId);
    }

    [Fact]
    public async Task RegisterDevice_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetUserId()).Returns(0); // No valid user

        var request = new RegisterDeviceRequest
        {
            DeviceId = "device-test-123",
            Platform = "ANDROID",
            DeviceModel = "OnePlus 11",
            OsVersion = "Android 14",
            AppVersion = "2.0.0",
            FcmToken = new string('x', 100)
        };

        // Act
        var result = await _controller.RegisterDevice(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task DeregisterDevice_ValidRequest_ReturnsOk()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetUserId()).Returns(42);

        _mockMediator.Setup(m => m.Send(It.IsAny<DeregisterDeviceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeregisterDevice(new DeregisterDeviceRequest { DeviceId = "device-test-123" }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<bool>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.True(apiResponse.Data);
    }

    [Fact]
    public async Task GetActiveDevice_WhenDeviceExists_ReturnsOk()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetUserId()).Returns(42);

        var activeDevice = new DeviceRegistrationResponse
        {
            DeviceRegistrationId = 1,
            EmployeeId = 42,
            DeviceId = "device-test-123",
            Platform = "ANDROID",
            IsActive = true
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<GetActiveDeviceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeDevice);

        // Act
        var result = await _controller.GetActiveDevice(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<DeviceRegistrationResponse>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal("device-test-123", apiResponse.Data?.DeviceId);
    }

    [Fact]
    public async Task GetActiveDevice_WhenNoDevice_ReturnsNotFound()
    {
        // Arrange
        _mockUserService.Setup(u => u.GetUserId()).Returns(42);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetActiveDeviceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceRegistrationResponse?)null);

        // Act
        var result = await _controller.GetActiveDevice(CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.False(apiResponse.Success);
    }
}

