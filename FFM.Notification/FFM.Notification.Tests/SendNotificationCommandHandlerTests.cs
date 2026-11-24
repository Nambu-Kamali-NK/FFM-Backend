using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Handlers;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Services;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class SendNotificationCommandHandlerTests
{
    private readonly Mock<INotificationTemplateRepository> _templateRepoMock;
    private readonly Mock<IEmployeeDeviceRepository> _deviceRepoMock;
    private readonly Mock<INotificationLogRepository> _logRepoMock;
    private readonly Mock<ITemplateInterpolationEngine> _interpolationEngineMock;
    private readonly Mock<IPushNotificationDispatcher> _pushDispatcherMock;
    private readonly SendNotificationCommandHandler _handler;

    public SendNotificationCommandHandlerTests()
    {
        _templateRepoMock = new Mock<INotificationTemplateRepository>();
        _deviceRepoMock = new Mock<IEmployeeDeviceRepository>();
        _logRepoMock = new Mock<INotificationLogRepository>();
        _interpolationEngineMock = new Mock<ITemplateInterpolationEngine>();
        _pushDispatcherMock = new Mock<IPushNotificationDispatcher>();

        _handler = new SendNotificationCommandHandler(
            _templateRepoMock.Object,
            _deviceRepoMock.Object,
            _logRepoMock.Object,
            _interpolationEngineMock.Object,
            _pushDispatcherMock.Object,
            NullLogger<SendNotificationCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenTemplateNotFound_ThrowsKeyNotFoundException()
    {
        _templateRepoMock.Setup(r => r.GetByCodeAndChannelAsync("UNKNOWN", "PUSH", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationTemplate?)null);

        var command = new SendNotificationCommand
        {
            RecipientEmployeeId = 1,
            TemplateCode = "UNKNOWN",
            Channel = "PUSH"
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenNoActiveDevice_ReturnsNoActiveDeviceStatus()
    {
        var template = new NotificationTemplate("TASK_ASSIGNED", "PUSH", "Title", "Body {{Task}}", "Desc", 1);
        _templateRepoMock.Setup(r => r.GetByCodeAndChannelAsync("TASK_ASSIGNED", "PUSH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _deviceRepoMock.Setup(r => r.GetActiveByEmployeeIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeDevice?)null);

        _interpolationEngineMock.Setup(e => e.Interpolate(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .Returns("Body TSK-1");

        var command = new SendNotificationCommand
        {
            RecipientEmployeeId = 101,
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("NO_ACTIVE_DEVICE", result.Status);
        Assert.Null(result.FcmMessageId);
        _logRepoMock.Verify(l => l.AddAsync(It.Is<NotificationLog>(n => n.Status == "NO_ACTIVE_DEVICE"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenActiveDeviceExists_DispatchesPushSuccessfully()
    {
        var template = new NotificationTemplate("TASK_ASSIGNED", "PUSH", "Title {{Task}}", "Body {{Task}}", "Desc", 1);
        _templateRepoMock.Setup(r => r.GetByCodeAndChannelAsync("TASK_ASSIGNED", "PUSH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var device = new EmployeeDevice(101, "DEV-1", "ANDROID", "Pixel 8", "14", "1.0", "fcm_test_token_123");
        _deviceRepoMock.Setup(r => r.GetActiveByEmployeeIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        _interpolationEngineMock.Setup(e => e.Interpolate(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .Returns("Rendered Text");

        _pushDispatcherMock.Setup(p => p.SendAsync("fcm_test_token_123", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushDispatchResult.Ok("projects/ffm/messages/msg_999"));

        var command = new SendNotificationCommand
        {
            RecipientEmployeeId = 101,
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH",
            Placeholders = new Dictionary<string, string> { { "Task", "TSK-100" } }
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("DELIVERED", result.Status);
        Assert.Equal("projects/ffm/messages/msg_999", result.FcmMessageId);
        _logRepoMock.Verify(l => l.AddAsync(It.Is<NotificationLog>(n => n.Status == "DELIVERED"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
