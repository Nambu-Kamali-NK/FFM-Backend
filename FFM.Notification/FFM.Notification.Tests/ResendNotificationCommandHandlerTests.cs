using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Handlers;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Validators;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class ResendNotificationCommandHandlerTests
{
    private readonly Mock<INotificationLogRepository> _logRepoMock;
    private readonly Mock<IEmployeeDeviceRepository> _deviceRepoMock;
    private readonly Mock<IPushNotificationDispatcher> _pushDispatcherMock;
    private readonly Mock<ILogger<ResendNotificationCommandHandler>> _loggerMock;
    private readonly ResendNotificationCommandHandler _handler;
    private readonly ResendNotificationCommandValidator _validator;

    public ResendNotificationCommandHandlerTests()
    {
        _logRepoMock = new Mock<INotificationLogRepository>();
        _deviceRepoMock = new Mock<IEmployeeDeviceRepository>();
        _pushDispatcherMock = new Mock<IPushNotificationDispatcher>();
        _loggerMock = new Mock<ILogger<ResendNotificationCommandHandler>>();

        _handler = new ResendNotificationCommandHandler(
            _logRepoMock.Object,
            _deviceRepoMock.Object,
            _pushDispatcherMock.Object,
            _loggerMock.Object);

        _validator = new ResendNotificationCommandValidator();
    }

    [Fact]
    public async Task Handle_SingleFailedNotification_WhenDeviceActive_DispatchesAndMarksDelivered()
    {
        // Arrange
        var log = NotificationLog.CreateFailed(101, "TASK_ASSIGNED", "PUSH", "Task Alert", "You have a new task", "Network timeout");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 20);

        var device = new EmployeeDevice(101, "device-1", "ANDROID", "Pixel 7", "14.0", "1.0.0", "valid-fcm-token");

        _logRepoMock.Setup(r => r.GetByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _deviceRepoMock.Setup(r => r.GetActiveByEmployeeIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        _pushDispatcherMock.Setup(d => d.SendAsync(
                "valid-fcm-token",
                "Task Alert",
                "You have a new task",
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushDispatchResult.Ok("projects/ffm/messages/retry-msg-99"));
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ResendNotificationCommand { NotificationId = 20 };

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, response.TotalProcessed);
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);

        var item = response.Results[0];
        Assert.Equal(20, item.NotificationId);
        Assert.Equal("FAILED", item.PreviousStatus);
        Assert.Equal("DELIVERED", item.CurrentStatus);
        Assert.Equal("projects/ffm/messages/retry-msg-99", item.FcmMessageId);
        Assert.True(item.Success);
        Assert.Null(item.ErrorMessage);

        Assert.Equal("DELIVERED", log.Status);
        Assert.Equal("projects/ffm/messages/retry-msg-99", log.FcmMessageId);
        _logRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDeliveredAndForceFalse_SkipsRedispatch()
    {
        // Arrange
        var log = NotificationLog.CreateDelivered(101, "TASK_ASSIGNED", "PUSH", "Title", "Body", "original-fcm-id");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 25);

        _logRepoMock.Setup(r => r.GetByIdAsync(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        var command = new ResendNotificationCommand { NotificationId = 25, Force = false };

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, response.TotalProcessed);
        Assert.Equal(1, response.SuccessCount);
        var item = response.Results[0];
        Assert.Equal("DELIVERED", item.CurrentStatus);
        Assert.Contains("already delivered", item.ErrorMessage);

        _pushDispatcherMock.Verify(d => d.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDeliveredAndForceTrue_ForcesRedispatch()
    {
        // Arrange
        var log = NotificationLog.CreateDelivered(101, "TASK_ASSIGNED", "PUSH", "Title", "Body", "original-fcm-id");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 25);

        var device = new EmployeeDevice(101, "device-1", "ANDROID", "Pixel 7", "14.0", "1.0.0", "valid-fcm-token");

        _logRepoMock.Setup(r => r.GetByIdAsync(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _deviceRepoMock.Setup(r => r.GetActiveByEmployeeIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        _pushDispatcherMock.Setup(d => d.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushDispatchResult.Ok("projects/ffm/messages/forced-msg-1"));
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ResendNotificationCommand { NotificationId = 25, Force = true };

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, response.TotalProcessed);
        Assert.Equal(1, response.SuccessCount);
        var item = response.Results[0];
        Assert.Equal("DELIVERED", item.CurrentStatus);
        Assert.Equal("projects/ffm/messages/forced-msg-1", item.FcmMessageId);
        _pushDispatcherMock.Verify(d => d.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoActiveDeviceFound_MarksNoActiveDevice()
    {
        // Arrange
        var log = NotificationLog.CreateFailed(102, "TASK_ASSIGNED", "PUSH", "Title", "Body", "Prev Error");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 30);

        _logRepoMock.Setup(r => r.GetByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _deviceRepoMock.Setup(r => r.GetActiveByEmployeeIdAsync(102, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeDevice?)null);
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ResendNotificationCommand { NotificationId = 30 };

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, response.TotalProcessed);
        Assert.Equal(0, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        var item = response.Results[0];
        Assert.Equal("NO_ACTIVE_DEVICE", item.CurrentStatus);
        Assert.False(item.Success);
        Assert.Contains("no active registered device", item.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenPushDispatcherFails_MarksFailed()
    {
        // Arrange
        var log = NotificationLog.CreateFailed(103, "TASK_ASSIGNED", "PUSH", "Title", "Body", "Prev Error");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 35);

        var device = new EmployeeDevice(103, "device-1", "ANDROID", "Pixel 7", "14.0", "1.0.0", "valid-fcm-token");

        _logRepoMock.Setup(r => r.GetByIdAsync(35, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _deviceRepoMock.Setup(r => r.GetActiveByEmployeeIdAsync(103, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        _pushDispatcherMock.Setup(d => d.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushDispatchResult.Fail("QUOTA_EXCEEDED", "FCM rate limit hit."));
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ResendNotificationCommand { NotificationId = 35 };

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, response.FailureCount);
        var item = response.Results[0];
        Assert.Equal("FAILED", item.CurrentStatus);
        Assert.False(item.Success);
        Assert.Equal("FCM rate limit hit.", item.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenNotificationNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _logRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog?)null);

        var command = new ResendNotificationCommand { NotificationId = 999 };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Validator_WhenNoIdProvided_ReturnsError()
    {
        var command = new ResendNotificationCommand();
        var result = await _validator.ValidateAsync(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_WhenBatchSizeExceeds50_ReturnsError()
    {
        var command = new ResendNotificationCommand
        {
            NotificationIds = Enumerable.Range(1, 51).ToList()
        };
        var result = await _validator.ValidateAsync(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("cannot exceed 50 items"));
    }
}
