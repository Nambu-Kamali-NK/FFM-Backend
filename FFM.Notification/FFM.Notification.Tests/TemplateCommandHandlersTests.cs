using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Handlers;
using FFM.Notification.Application.Queries;
using FFM.Notification.Application.Services;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FFM.Notification.Tests;

public class TemplateCommandHandlersTests
{
    private readonly Mock<INotificationTemplateRepository> _mockRepo;
    private readonly Mock<ILogger<CreateNotificationTemplateCommandHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<UpdateNotificationTemplateCommandHandler>> _mockUpdateLogger;
    private readonly ITemplateInterpolationEngine _engine;

    public TemplateCommandHandlersTests()
    {
        _mockRepo = new Mock<INotificationTemplateRepository>();
        _mockCreateLogger = new Mock<ILogger<CreateNotificationTemplateCommandHandler>>();
        _mockUpdateLogger = new Mock<ILogger<UpdateNotificationTemplateCommandHandler>>();
        _engine = new TemplateInterpolationEngine();
    }

    [Fact]
    public async Task CreateTemplate_NewTemplate_Succeeds()
    {
        var handler = new CreateNotificationTemplateCommandHandler(_mockRepo.Object, _mockCreateLogger.Object);
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "TASK_ASSIGNED",
            Channel: "PUSH",
            Title: "New Task: {{TaskNumber}}",
            BodyTemplate: "Hi {{EmployeeName}}",
            Description: "Alert for task",
            CreatedByUserId: 1
        );

        _mockRepo.Setup(r => r.GetByCodeAndChannelAsync("TASK_ASSIGNED", "PUSH", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationTemplate?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("TASK_ASSIGNED", result.TemplateCode);
        Assert.Equal("PUSH", result.Channel);
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<NotificationTemplate>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTemplate_DuplicateCodeAndChannel_ThrowsInvalidOperationException()
    {
        var handler = new CreateNotificationTemplateCommandHandler(_mockRepo.Object, _mockCreateLogger.Object);
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "TASK_ASSIGNED",
            Channel: "PUSH",
            Title: "Duplicate Title",
            BodyTemplate: "Body",
            Description: null,
            CreatedByUserId: 1
        );

        var existing = new NotificationTemplate("TASK_ASSIGNED", "PUSH", "Title", "Body", null, 1);
        _mockRepo.Setup(r => r.GetByCodeAndChannelAsync("TASK_ASSIGNED", "PUSH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task RenderTemplate_ExistingActiveTemplate_InterpolatesCleanly()
    {
        var handler = new RenderTemplateQueryHandler(_mockRepo.Object, _engine);

        var template = new NotificationTemplate(
            "TASK_ASSIGNED",
            "PUSH",
            "Task {{TaskNumber}}",
            "Hello {{EmployeeName}}, visit {{SiteName}}.",
            null,
            1
        );

        _mockRepo.Setup(r => r.GetByCodeAndChannelAsync("TASK_ASSIGNED", "PUSH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var query = new RenderTemplateQuery(
            "TASK_ASSIGNED",
            "PUSH",
            new Dictionary<string, string>
            {
                ["TaskNumber"] = "TK-101",
                ["EmployeeName"] = "Ramesh",
                ["SiteName"] = "Bank ATM"
            }
        );

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Task TK-101", result.RenderedTitle);
        Assert.Equal("Hello Ramesh, visit Bank ATM.", result.RenderedBody);
    }
}

