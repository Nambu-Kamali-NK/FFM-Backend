using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Handlers;
using FFM.Notification.Application.Validators;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class MarkNotificationReadCommandHandlerTests
{
    private readonly Mock<INotificationLogRepository> _logRepoMock;
    private readonly Mock<ILogger<MarkNotificationReadCommandHandler>> _loggerMock;
    private readonly MarkNotificationReadCommandHandler _handler;
    private readonly MarkNotificationReadCommandValidator _validator;

    public MarkNotificationReadCommandHandlerTests()
    {
        _logRepoMock = new Mock<INotificationLogRepository>();
        _loggerMock = new Mock<ILogger<MarkNotificationReadCommandHandler>>();
        _handler = new MarkNotificationReadCommandHandler(_logRepoMock.Object, _loggerMock.Object);
        _validator = new MarkNotificationReadCommandValidator();
    }

    [Fact]
    public async Task Handle_SingleNotification_MarksAsReadSuccessfully()
    {
        // Arrange
        var log = NotificationLog.CreateDelivered(101, "TASK_ASSIGNED", "PUSH", "Title", "Body", "msg-1");
        // Ensure log has an ID by reflection or constructor
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 55);

        _logRepoMock.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(55)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog> { log });
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new MarkNotificationReadCommand { NotificationId = 55 };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Contains(55, result.UpdatedIds);
        Assert.True(log.IsRead);
        Assert.NotNull(log.ReadAt);
        _logRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SingleNotification_WhenNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _logRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog>());

        var command = new MarkNotificationReadCommand { NotificationId = 999 };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SingleNotification_AlreadyRead_IsIdempotent()
    {
        // Arrange
        var initialReadAt = DateTime.UtcNow.AddMinutes(-10);
        var log = new NotificationLog(101, "TASK_ASSIGNED", "PUSH", "Title", "Body", "msg-1", "DELIVERED", null, true, initialReadAt);
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log, 77);

        _logRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog> { log });

        var command = new MarkNotificationReadCommand { NotificationId = 77 };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(initialReadAt, log.ReadAt); // Timestamp preserved
    }

    [Fact]
    public async Task Handle_BatchNotificationIds_MarksAllAsRead()
    {
        // Arrange
        var log1 = NotificationLog.CreateDelivered(101, "TASK_ASSIGNED", "PUSH", "T1", "B1", "msg-1");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log1, 10);
        var log2 = NotificationLog.CreateDelivered(101, "TASK_CANCELLED", "PUSH", "T2", "B2", "msg-2");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log2, 11);

        _logRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog> { log1, log2 });
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var command = new MarkNotificationReadCommand { NotificationIds = new List<int> { 10, 11 } };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.UpdatedCount);
        Assert.True(log1.IsRead);
        Assert.True(log2.IsRead);
        _logRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MarkAllForRecipient_MarksAllUnreadLogs()
    {
        // Arrange
        var log1 = NotificationLog.CreateDelivered(200, "T1", "PUSH", "T1", "B1", "m1");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log1, 1);
        var log2 = NotificationLog.CreateDelivered(200, "T2", "PUSH", "T2", "B2", "m2");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log2, 2);

        _logRepoMock.Setup(r => r.GetUnreadByRecipientAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog> { log1, log2 });
        _logRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var command = new MarkNotificationReadCommand { RecipientEmployeeId = 200, MarkAll = true };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.UpdatedCount);
        Assert.True(log1.IsRead);
        Assert.True(log2.IsRead);
        _logRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenEmptyCommand_ReturnsValidationError()
    {
        var command = new MarkNotificationReadCommand();
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("You must specify either a notificationId"));
    }

    [Fact]
    public void Validator_WhenMarkAllWithoutRecipient_ReturnsValidationError()
    {
        var command = new MarkNotificationReadCommand { MarkAll = true, RecipientEmployeeId = null };
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("RecipientEmployeeId is required"));
    }

    [Fact]
    public void Validator_WhenValidNotificationId_Passes()
    {
        var command = new MarkNotificationReadCommand { NotificationId = 50 };
        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
