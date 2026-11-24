using FFM.Notification.Application.Commands;
using FluentValidation;

namespace FFM.Notification.Application.Validators;

public class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    private static readonly string[] AllowedChannels = { "PUSH", "SMS", "EMAIL", "IN_APP" };

    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.RecipientEmployeeId)
            .GreaterThan(0)
            .WithMessage("Recipient Employee ID must be a positive integer.");

        RuleFor(x => x.TemplateCode)
            .NotEmpty()
            .WithMessage("Template code is required.")
            .MaximumLength(50)
            .WithMessage("Template code must not exceed 50 characters.");

        RuleFor(x => x.Channel)
            .NotEmpty()
            .WithMessage("Channel is required.")
            .Must(channel => AllowedChannels.Contains(channel.Trim().ToUpperInvariant()))
            .WithMessage("Channel must be one of: PUSH, SMS, EMAIL, IN_APP.");
    }
}

