using System.Linq;
using FFM.Notification.Application.Commands;
using FluentValidation;

namespace FFM.Notification.Application.Validators;

public class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.NotificationId.HasValue || (x.NotificationIds != null && x.NotificationIds.Any()) || (x.MarkAll && x.RecipientEmployeeId.HasValue))
            .WithMessage("You must specify either a notificationId, a non-empty notificationIds list, or recipientEmployeeId with markAll=true.");

        When(x => x.NotificationId.HasValue, () =>
        {
            RuleFor(x => x.NotificationId!.Value)
                .GreaterThan(0)
                .WithMessage("NotificationId must be greater than 0.");
        });

        When(x => x.NotificationIds != null && x.NotificationIds.Any(), () =>
        {
            RuleFor(x => x.NotificationIds!.Count)
                .LessThanOrEqualTo(100)
                .WithMessage("NotificationIds batch size cannot exceed 100 items.");

            RuleForEach(x => x.NotificationIds!)
                .GreaterThan(0)
                .WithMessage("Each NotificationId must be greater than 0.");
        });

        When(x => x.MarkAll, () =>
        {
            RuleFor(x => x.RecipientEmployeeId)
                .NotNull()
                .WithMessage("RecipientEmployeeId is required when markAll is true.")
                .GreaterThan(0)
                .WithMessage("RecipientEmployeeId must be greater than 0.");
        });
    }
}
