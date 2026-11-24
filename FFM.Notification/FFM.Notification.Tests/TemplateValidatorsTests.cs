using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Validators;
using Xunit;

namespace FFM.Notification.Tests;

public class TemplateValidatorsTests
{
    private readonly CreateNotificationTemplateCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidPushTemplate_PassesValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "TASK_ASSIGNED",
            Channel: "PUSH",
            Title: "New Task: {{TaskNumber}}",
            BodyTemplate: "Hi {{EmployeeName}}, new task assigned at {{SiteName}}.",
            Description: "Task assignment push alert",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidSmsTemplate_PassesValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "OTP_ALERT",
            Channel: "SMS",
            Title: null, // Title optional for SMS
            BodyTemplate: "Your FFM verification code is {{Code}}.",
            Description: "SMS OTP",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnbalancedBraces_FailsValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "TASK_ALERT",
            Channel: "PUSH",
            Title: "Task Alert",
            BodyTemplate: "Hi {{EmployeeName, you have a task.", // Missing closing }}
            Description: "Unbalanced template",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "BodyTemplate");
    }

    [Fact]
    public void Validate_UnsupportedChannel_FailsValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "TASK_ALERT",
            Channel: "WHATSAPP", // Not in PUSH, IN_APP, SMS, EMAIL
            Title: "Task Alert",
            BodyTemplate: "Hello world",
            Description: "Invalid channel test",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Channel");
    }

    [Fact]
    public void Validate_PushBodyExceeding500Chars_FailsValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "LONG_PUSH",
            Channel: "PUSH",
            Title: "Title",
            BodyTemplate: new string('A', 501),
            Description: "Too long push body",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "BodyTemplate");
    }

    [Fact]
    public void Validate_SmsBodyExceeding160Chars_FailsValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "LONG_SMS",
            Channel: "SMS",
            Title: null,
            BodyTemplate: new string('B', 161),
            Description: "Too long SMS body",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "BodyTemplate");
    }

    [Fact]
    public void Validate_PushWithoutTitle_FailsValidation()
    {
        var command = new CreateNotificationTemplateCommand(
            TemplateCode: "NO_TITLE_PUSH",
            Channel: "PUSH",
            Title: "", // Mandatory for push
            BodyTemplate: "Valid body text",
            Description: "Push missing title",
            CreatedByUserId: 1
        );

        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
    }
}

