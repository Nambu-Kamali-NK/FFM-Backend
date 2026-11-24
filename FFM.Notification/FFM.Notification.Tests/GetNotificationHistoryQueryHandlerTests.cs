using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Application.Handlers;
using FFM.Notification.Application.Queries;
using FFM.Notification.Application.Validators;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class GetNotificationHistoryQueryHandlerTests
{
    private readonly Mock<INotificationLogRepository> _logRepoMock;
    private readonly Mock<ILogger<GetNotificationHistoryQueryHandler>> _loggerMock;
    private readonly GetNotificationHistoryQueryHandler _handler;
    private readonly GetNotificationHistoryQueryValidator _validator;

    public GetNotificationHistoryQueryHandlerTests()
    {
        _logRepoMock = new Mock<INotificationLogRepository>();
        _loggerMock = new Mock<ILogger<GetNotificationHistoryQueryHandler>>();
        _handler = new GetNotificationHistoryQueryHandler(_logRepoMock.Object, _loggerMock.Object);
        _validator = new GetNotificationHistoryQueryValidator();
    }

    [Fact]
    public async Task Handle_ReturnsPagedNotificationLogsWithUnreadCount()
    {
        // Arrange
        var log1 = NotificationLog.CreateDelivered(101, "TASK_ASSIGNED", "PUSH", "Title 1", "Body 1", "msg-1");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log1, 1);
        var log2 = NotificationLog.CreateDelivered(101, "TASK_CANCELLED", "PUSH", "Title 2", "Body 2", "msg-2");
        typeof(NotificationLog).GetProperty("Id")?.SetValue(log2, 2);

        var query = new GetNotificationHistoryQuery
        {
            RecipientEmployeeId = 101,
            IsRead = false,
            Page = 1,
            PageSize = 10
        };

        _logRepoMock.Setup(r => r.GetHistoryPagedAsync(
            101, false, null, null, null, 1, 10, "dispatchedat", "desc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<NotificationLog> { log1, log2 }, 2, 2));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.UnreadCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal("TASK_ASSIGNED", result.Items[0].TemplateCode);
    }

    [Fact]
    public void Validator_WhenPageLessThanOne_ReturnsError()
    {
        var query = new GetNotificationHistoryQuery { Page = 0 };
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Page");
    }

    [Fact]
    public void Validator_WhenPageSizeExceeds100_ReturnsError()
    {
        var query = new GetNotificationHistoryQuery { PageSize = 101 };
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_WhenInvalidSortOrder_ReturnsError()
    {
        var query = new GetNotificationHistoryQuery { SortOrder = "sideways" };
        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SortOrder");
    }

    [Fact]
    public void Validator_WhenValidParameters_Passes()
    {
        var query = new GetNotificationHistoryQuery
        {
            RecipientEmployeeId = 1,
            Page = 1,
            PageSize = 20,
            SortBy = "dispatchedat",
            SortOrder = "desc",
            Status = "DELIVERED",
            Channel = "PUSH"
        };
        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
