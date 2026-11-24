using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Handlers;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class RegisterDeviceCommandHandlerTests
{
    private readonly Mock<IEmployeeDeviceRepository> _mockRepo;
    private readonly Mock<ILogger<RegisterDeviceCommandHandler>> _mockLogger;
    private readonly RegisterDeviceCommandHandler _handler;

    public RegisterDeviceCommandHandlerTests()
    {
        _mockRepo = new Mock<IEmployeeDeviceRepository>();
        _mockLogger = new Mock<ILogger<RegisterDeviceCommandHandler>>();
        _handler = new RegisterDeviceCommandHandler(_mockRepo.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_NewDeviceRegistration_AddsDeviceSuccessfully()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            EmployeeId: 101,
            DeviceId: "device-uuid-12345",
            Platform: "ANDROID",
            DeviceModel: "Samsung S23",
            OsVersion: "Android 14",
            AppVersion: "1.0.0",
            FcmToken: new string('a', 150)
        );

        _mockRepo.Setup(r => r.GetByDeviceIdAsync("device-uuid-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeDevice?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.EmployeeId);
        Assert.Equal("device-uuid-12345", result.DeviceId);
        Assert.Equal("ANDROID", result.Platform);
        Assert.True(result.IsActive);

        _mockRepo.Verify(r => r.AddAsync(It.Is<EmployeeDevice>(d => d.DeviceId == "device-uuid-12345" && d.EmployeeId == 101), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.DeactivateOtherDevicesForEmployeeAsync(101, "device-uuid-12345", "ANDROID", It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingDeviceSameEmployee_UpdatesTokenAtomically()
    {
        // Arrange
        var existingDevice = new EmployeeDevice(
            101,
            "device-uuid-12345",
            "ANDROID",
            "Samsung S23",
            "Android 13",
            "0.9.0",
            new string('x', 150)
        );

        var newFcmToken = new string('z', 160);
        var command = new RegisterDeviceCommand(
            EmployeeId: 101,
            DeviceId: "device-uuid-12345",
            Platform: "ANDROID",
            DeviceModel: "Samsung S23",
            OsVersion: "Android 14",
            AppVersion: "1.0.0",
            FcmToken: newFcmToken
        );

        _mockRepo.Setup(r => r.GetByDeviceIdAsync("device-uuid-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.EmployeeId);
        Assert.Equal("1.0.0", result.AppVersion);
        Assert.True(result.IsActive);
        Assert.Equal(newFcmToken, existingDevice.FcmToken);

        _mockRepo.Verify(r => r.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SharedDeviceHandover_ReassignsDeviceToNewEmployee()
    {
        // Arrange - Device previously belonged to Employee 50
        var existingDevice = new EmployeeDevice(
            50,
            "device-handover-999",
            "ANDROID",
            "Zebra TC57",
            "Android 12",
            "1.0.0",
            new string('a', 150)
        );

        // Employee 101 logs in on the same shared company device
        var command = new RegisterDeviceCommand(
            EmployeeId: 101,
            DeviceId: "device-handover-999",
            Platform: "ANDROID",
            DeviceModel: "Zebra TC57",
            OsVersion: "Android 12",
            AppVersion: "1.0.1",
            FcmToken: new string('b', 150)
        );

        _mockRepo.Setup(r => r.GetByDeviceIdAsync("device-handover-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.EmployeeId);
        Assert.Equal(101, existingDevice.EmployeeId); // Reassigned!
        Assert.True(result.IsActive);

        _mockRepo.Verify(r => r.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeregisterDevice_SetsIsActiveToFalse()
    {
        // Arrange
        var mockDeregisterLogger = new Mock<ILogger<DeregisterDeviceCommandHandler>>();
        var deregisterHandler = new DeregisterDeviceCommandHandler(_mockRepo.Object, mockDeregisterLogger.Object);

        var existingDevice = new EmployeeDevice(
            101,
            "device-to-logout",
            "ANDROID",
            "Pixel 8",
            "Android 14",
            "1.0.0",
            new string('a', 150)
        );

        _mockRepo.Setup(r => r.GetByDeviceIdAsync("device-to-logout", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await deregisterHandler.Handle(new DeregisterDeviceCommand(101, "device-to-logout"), CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.False(existingDevice.IsActive);
        _mockRepo.Verify(r => r.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

