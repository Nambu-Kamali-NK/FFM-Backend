using FFM.Notification.Application.Queries;
using FluentValidation;

namespace FFM.Notification.Application.Validators;

public class GetUnreadNotificationCountQueryValidator : AbstractValidator<GetUnreadNotificationCountQuery>
{
    public GetUnreadNotificationCountQueryValidator()
    {
        RuleFor(q => q.RecipientEmployeeId)
            .GreaterThan(0)
            .When(q => q.RecipientEmployeeId.HasValue)
            .WithMessage("recipientEmployeeId must be a positive integer greater than zero.");
    }
}
