using System.Linq;
using FFM.Notification.Application.Commands;
using FluentValidation;

namespace FFM.Notification.Application.Validators;

public class ResendNotificationCommandValidator : AbstractValidator<ResendNotificationCommand>
{
    public ResendNotificationCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.NotificationId.HasValue && x.NotificationId.Value > 0) ||
                       (x.NotificationIds != null && x.NotificationIds.Any(id => id > 0)))
            .WithMessage("At least one valid NotificationId (> 0) or non-empty NotificationIds list must be provided.");

        When(x => x.NotificationId.HasValue, () =>
        {
            RuleFor(x => x.NotificationId!.Value)
                .GreaterThan(0)
                .WithMessage("NotificationId must be greater than 0.");
        });

        When(x => x.NotificationIds != null && x.NotificationIds.Any(), () =>
        {
            RuleFor(x => x.NotificationIds!)
                .Must(list => list.Count <= 50)
                .WithMessage("NotificationIds batch size cannot exceed 50 items.")
                .Must(list => list.All(id => id > 0))
                .WithMessage("All IDs in NotificationIds list must be greater than 0.");
        });
    }
}
