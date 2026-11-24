using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFM.Notification.Api.Controllers;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Interfaces;
using FFM.Notification.Application.Queries;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class DispatchControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IValidator<SendNotificationCommand>> _sendValidatorMock;
    private readonly Mock<IValidator<MarkNotificationReadCommand>> _readValidatorMock;
    private readonly Mock<IValidator<GetNotificationHistoryQuery>> _historyValidatorMock;
    private readonly Mock<IValidator<ResendNotificationCommand>> _resendValidatorMock;
    private readonly Mock<IValidator<GetUnreadNotificationCountQuery>> _unreadCountValidatorMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly DispatchController _controller;

    public DispatchControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _sendValidatorMock = new Mock<IValidator<SendNotificationCommand>>();
        _readValidatorMock = new Mock<IValidator<MarkNotificationReadCommand>>();
        _historyValidatorMock = new Mock<IValidator<GetNotificationHistoryQuery>>();
        _resendValidatorMock = new Mock<IValidator<ResendNotificationCommand>>();
        _unreadCountValidatorMock = new Mock<IValidator<GetUnreadNotificationCountQuery>>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        _controller = new DispatchController(
            _mediatorMock.Object,
            _sendValidatorMock.Object,
            _readValidatorMock.Object,
            _historyValidatorMock.Object,
            _resendValidatorMock.Object,
            _unreadCountValidatorMock.Object,
            _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task SendNotification_WhenRequestNull_ReturnsBadRequest()
    {
        var result = await _controller.SendNotification(null!, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("INVALID_PAYLOAD", apiResp.Error?.Code);
    }

    [Fact]
    public async Task SendNotification_WhenValidationFails_ReturnsBadRequest()
    {
        var request = new SendNotificationRequest
        {
            RecipientEmployeeId = 0,
            TemplateCode = "",
            Channel = "INVALID"
        };

        var validationFailures = new List<ValidationFailure>
        {
            new("RecipientEmployeeId", "Recipient Employee ID must be a positive integer.")
        };

        _sendValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var result = await _controller.SendNotification(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("VALIDATION_ERROR", apiResp.Error?.Code);
    }

    [Fact]
    public async Task SendNotification_WhenTemplateNotFound_ReturnsNotFound()
    {
        var request = new SendNotificationRequest
        {
            RecipientEmployeeId = 1,
            TemplateCode = "NON_EXISTENT",
            Channel = "PUSH"
        };

        _sendValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mediatorMock.Setup(m => m.Send(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Template not found"));

        var result = await _controller.SendNotification(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("TEMPLATE_NOT_FOUND", apiResp.Error?.Code);
    }

    [Fact]
    public async Task SendNotification_WhenValid_ReturnsOkWithDispatchedDetails()
    {
        var request = new SendNotificationRequest
        {
            RecipientEmployeeId = 1,
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH",
            Placeholders = new Dictionary<string, string> { { "TaskNumber", "TSK-101" } }
        };

        _sendValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var expectedResponse = new SendNotificationResponse
        {
            NotificationLogId = 10,
            RecipientEmployeeId = 1,
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH",
            RenderedTitle = "Urgent Assignment: TSK-101",
            RenderedBody = "Task details here",
            Status = "DELIVERED",
            FcmMessageId = "projects/ffm-notification-api/messages/mock_123",
            DispatchedAt = DateTime.UtcNow
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.SendNotification(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<SendNotificationResponse>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.NotNull(apiResp.Data);
        Assert.Equal("DELIVERED", apiResp.Data.Status);
        Assert.Equal("projects/ffm-notification-api/messages/mock_123", apiResp.Data.FcmMessageId);
    }

    [Fact]
    public async Task MarkAsRead_WhenRequestNull_ReturnsBadRequest()
    {
        var result = await _controller.MarkAsRead(null!, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("INVALID_PAYLOAD", apiResp.Error?.Code);
    }

    [Fact]
    public async Task MarkAsRead_WhenValidationFails_ReturnsBadRequest()
    {
        var request = new MarkNotificationReadRequest();
        var validationFailures = new List<ValidationFailure>
        {
            new("Payload", "You must specify either a notificationId, a non-empty notificationIds list, or recipientEmployeeId with markAll=true.")
        };

        _readValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var result = await _controller.MarkAsRead(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("VALIDATION_ERROR", apiResp.Error?.Code);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationNotFound_ReturnsNotFound()
    {
        var request = new MarkNotificationReadRequest { NotificationId = 999 };

        _readValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mediatorMock.Setup(m => m.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Notification with ID 999 was not found."));

        var result = await _controller.MarkAsRead(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("NOTIFICATION_NOT_FOUND", apiResp.Error?.Code);
    }

    [Fact]
    public async Task MarkAsRead_WhenValid_ReturnsOkWithUpdatedCount()
    {
        var request = new MarkNotificationReadRequest { NotificationId = 101 };

        _readValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var expectedResponse = new MarkNotificationReadResponse
        {
            UpdatedCount = 1,
            ReadAt = DateTime.UtcNow,
            UpdatedIds = new List<int> { 101 }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.MarkAsRead(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<MarkNotificationReadResponse>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.NotNull(apiResp.Data);
        Assert.Equal(1, apiResp.Data.UpdatedCount);
        Assert.Contains(101, apiResp.Data.UpdatedIds);
    }

    [Fact]
    public async Task GetHistory_WhenValidationFails_ReturnsBadRequest()
    {
        var request = new GetNotificationHistoryRequest { Page = 0 };
        var validationFailures = new List<ValidationFailure>
        {
            new("Page", "Page must be greater than or equal to 1.")
        };

        _historyValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetNotificationHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var result = await _controller.GetHistory(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("VALIDATION_ERROR", apiResp.Error?.Code);
    }

    [Fact]
    public async Task GetHistory_WhenValid_ReturnsOkWithPagedResult()
    {
        var request = new GetNotificationHistoryRequest
        {
            RecipientEmployeeId = 1,
            Page = 1,
            PageSize = 10
        };

        _historyValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetNotificationHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var expectedResult = new NotificationHistoryPagedResult
        {
            Items = new List<NotificationLogDto>
            {
                new() { Id = 10, RecipientEmployeeId = 1, Title = "Alert", Body = "Test alert", IsRead = false }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 1,
            UnreadCount = 1
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetNotificationHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await _controller.GetHistory(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<NotificationHistoryPagedResult>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.NotNull(apiResp.Data);
        Assert.Equal(1, apiResp.Data.TotalCount);
        Assert.Equal(1, apiResp.Data.UnreadCount);
        Assert.Single(apiResp.Data.Items);
    }

    [Fact]
    public async Task ResendNotification_WhenRequestNull_ReturnsBadRequest()
    {
        var result = await _controller.ResendNotification(null!, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("INVALID_PAYLOAD", apiResp.Error?.Code);
    }

    [Fact]
    public async Task ResendNotification_WhenValidationFails_ReturnsBadRequest()
    {
        var request = new ResendNotificationRequest();

        _resendValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ResendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("NotificationId", "NotificationId required.") }));

        var result = await _controller.ResendNotification(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("VALIDATION_ERROR", apiResp.Error?.Code);
    }

    [Fact]
    public async Task ResendNotification_WhenSuccess_ReturnsOkWithResponse()
    {
        var request = new ResendNotificationRequest { NotificationId = 12 };

        _resendValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ResendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var expectedResponse = new ResendNotificationResponse
        {
            TotalProcessed = 1,
            SuccessCount = 1,
            FailureCount = 0,
            Results = new List<ResendNotificationItemResult>
            {
                new()
                {
                    NotificationId = 12,
                    RecipientEmployeeId = 1,
                    PreviousStatus = "FAILED",
                    CurrentStatus = "DELIVERED",
                    Success = true
                }
            }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.ResendNotification(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var apiResp = Assert.IsType<ApiResponse<ResendNotificationResponse>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.NotNull(apiResp.Data);
        Assert.Equal(1, apiResp.Data.SuccessCount);
    }

    [Fact]
    public async Task ResendNotification_WhenNotFound_ReturnsNotFound()
    {
        var request = new ResendNotificationRequest { NotificationId = 999 };

        _resendValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<ResendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Notification 999 not found"));

        var result = await _controller.ResendNotification(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }
}

