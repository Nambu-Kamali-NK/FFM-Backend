using FluentValidation;
using FFM.Notification.Application.Commands;
using FFM.Notification.Application.Queries;
using System.Text.RegularExpressions;

namespace FFM.Notification.Application.Validators;

public class CreateNotificationTemplateCommandValidator : AbstractValidator<CreateNotificationTemplateCommand>
{
    private static readonly string[] AllowedChannels = { "PUSH", "IN_APP", "SMS", "EMAIL" };

    public CreateNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateCode)
            .NotEmpty().WithMessage("TemplateCode is required.")
            .MinimumLength(3).WithMessage("TemplateCode must be at least 3 characters.")
            .MaximumLength(50).WithMessage("TemplateCode cannot exceed 50 characters.")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("TemplateCode must be alphanumeric with underscores only.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required.")
            .Must(c => AllowedChannels.Contains(c?.Trim().ToUpperInvariant()))
            .WithMessage("Unsupported channel. Supported values are: PUSH, IN_APP, SMS, EMAIL.");

        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("BodyTemplate is required.")
            .Must(HaveBalancedBraces).WithMessage("BodyTemplate contains malformed or unbalanced '{{' and '}}' placeholders.")
            .DependentRules(() =>
            {
                When(x => x.Channel?.Trim().ToUpperInvariant() == "PUSH", () =>
                {
                    RuleFor(x => x.BodyTemplate).MaximumLength(500).WithMessage("Push notification body cannot exceed 500 characters.");
                    RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required for PUSH notifications.").MaximumLength(150);
                });

                When(x => x.Channel?.Trim().ToUpperInvariant() == "SMS", () =>
                {
                    RuleFor(x => x.BodyTemplate).MaximumLength(160).WithMessage("SMS body cannot exceed 160 characters.");
                });

                When(x => x.Channel?.Trim().ToUpperInvariant() == "EMAIL", () =>
                {
                    RuleFor(x => x.BodyTemplate).MaximumLength(10000).WithMessage("Email HTML body cannot exceed 10,000 characters.");
                    RuleFor(x => x.Title).NotEmpty().WithMessage("Subject/Title is required for EMAIL.").MaximumLength(150);
                });
            });
    }

    private static bool HaveBalancedBraces(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        int openCount = Regex.Matches(text, @"\{\{").Count;
        int closeCount = Regex.Matches(text, @"\}\}").Count;
        return openCount == closeCount;
    }
}

public class UpdateNotificationTemplateCommandValidator : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("TemplateId must be a valid positive integer.");

        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("BodyTemplate is required.");
    }
}

public class RenderTemplateQueryValidator : AbstractValidator<RenderTemplateQuery>
{
    public RenderTemplateQueryValidator()
    {
        RuleFor(x => x.TemplateCode)
            .NotEmpty().WithMessage("TemplateCode is required.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required.");
    }
}

