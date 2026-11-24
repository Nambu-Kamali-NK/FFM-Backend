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

public class TemplatesControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly TemplatesController _controller;

    public TemplatesControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockUserService = new Mock<ICurrentUserService>();
        _controller = new TemplatesController(_mockMediator.Object, _mockUserService.Object);
    }

    [Fact]
    public async Task CreateTemplate_ValidRequest_ReturnsCreated()
    {
        _mockUserService.Setup(u => u.GetUserId()).Returns(1);

        var request = new CreateNotificationTemplateRequest
        {
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH",
            Title = "Task {{TaskNumber}}",
            BodyTemplate = "Hi {{EmployeeName}}",
            Description = "Task assignment"
        };

        var response = new NotificationTemplateResponse
        {
            TemplateId = 1,
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH",
            Title = "Task {{TaskNumber}}",
            BodyTemplate = "Hi {{EmployeeName}}",
            IsActive = true
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateNotificationTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.CreateTemplate(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var apiResp = Assert.IsType<ApiResponse<NotificationTemplateResponse>>(objectResult.Value);
        Assert.True(apiResp.Success);
        Assert.Equal("TASK_ASSIGNED", apiResp.Data?.TemplateCode);
    }

    [Fact]
    public async Task UpdateTemplate_ValidPayload_ReturnsOk()
    {
        var request = new UpdateNotificationTemplateRequest
        {
            TemplateId = 1,
            Title = "Updated Title",
            BodyTemplate = "Updated Body",
            Description = "Updated Desc",
            IsActive = true
        };

        var response = new NotificationTemplateResponse
        {
            TemplateId = 1,
            TemplateCode = "TASK_ASSIGNED",
            Channel = "PUSH",
            Title = "Updated Title",
            BodyTemplate = "Updated Body",
            Description = "Updated Desc",
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateNotificationTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.UpdateTemplate(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var apiResp = Assert.IsType<ApiResponse<NotificationTemplateResponse>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.Equal("Updated Title", apiResp.Data?.Title);
    }

    [Fact]
    public async Task UpdateTemplate_NullOrInvalidId_ReturnsBadRequest()
    {
        var request = new UpdateNotificationTemplateRequest
        {
            TemplateId = 0,
            Title = "Test"
        };

        var result = await _controller.UpdateTemplate(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResp = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(apiResp.Success);
    }

    [Fact]
    public async Task SearchTemplates_WhenQueryProvided_ReturnsMatchingTemplates()
    {
        var templates = new List<NotificationTemplateResponse>
        {
            new() { TemplateId = 1, TemplateCode = "TASK_ASSIGNED", Channel = "PUSH", Title = "Title 1" }
        };

        _mockMediator.Setup(m => m.Send(It.Is<SearchNotificationTemplatesQuery>(q => q.Query == "ASSIGNED"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var result = await _controller.SearchTemplates("ASSIGNED", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var apiResp = Assert.IsType<ApiResponse<List<NotificationTemplateResponse>>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.Single(apiResp.Data!);
    }

    [Fact]
    public async Task SearchTemplates_WhenNoQuery_ReturnsAllTemplates()
    {
        var templates = new List<NotificationTemplateResponse>
        {
            new() { TemplateId = 1, TemplateCode = "TASK_ASSIGNED", Channel = "PUSH", Title = "Title 1" },
            new() { TemplateId = 2, TemplateCode = "TASK_CANCELLED", Channel = "PUSH", Title = "Title 2" }
        };

        _mockMediator.Setup(m => m.Send(It.Is<SearchNotificationTemplatesQuery>(q => q.Query == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var result = await _controller.SearchTemplates(null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var apiResp = Assert.IsType<ApiResponse<List<NotificationTemplateResponse>>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.Equal(2, apiResp.Data?.Count);
    }
}

