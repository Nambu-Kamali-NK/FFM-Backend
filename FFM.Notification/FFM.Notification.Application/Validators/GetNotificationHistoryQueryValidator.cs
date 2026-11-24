using FFM.Notification.Application.Queries;
using FluentValidation;

namespace FFM.Notification.Application.Validators;

public class GetNotificationHistoryQueryValidator : AbstractValidator<GetNotificationHistoryQuery>
{
    private static readonly string[] AllowedSortOrders = { "asc", "desc" };
    private static readonly string[] AllowedSortFields = { "dispatchedat", "id", "recipientemployeeid" };
    private static readonly string[] AllowedStatuses = { "DELIVERED", "FAILED", "NO_ACTIVE_DEVICE" };
    private static readonly string[] AllowedChannels = { "PUSH", "EMAIL", "SMS", "IN_APP" };

    public GetNotificationHistoryQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        When(x => x.RecipientEmployeeId.HasValue, () =>
        {
            RuleFor(x => x.RecipientEmployeeId!.Value)
                .GreaterThan(0)
                .WithMessage("RecipientEmployeeId must be greater than 0.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.SortOrder), () =>
        {
            RuleFor(x => x.SortOrder)
                .Must(o => AllowedSortOrders.Contains(o.ToLowerInvariant()))
                .WithMessage("SortOrder must be either 'asc' or 'desc'.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.SortBy), () =>
        {
            RuleFor(x => x.SortBy)
                .Must(s => AllowedSortFields.Contains(s.ToLowerInvariant()))
                .WithMessage("SortBy must be one of: 'dispatchedat', 'id', 'recipientemployeeid'.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Status), () =>
        {
            RuleFor(x => x.Status)
                .Must(s => AllowedStatuses.Contains(s!.ToUpperInvariant()))
                .WithMessage("Status must be one of: 'DELIVERED', 'FAILED', 'NO_ACTIVE_DEVICE'.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Channel), () =>
        {
            RuleFor(x => x.Channel)
                .Must(c => AllowedChannels.Contains(c!.ToUpperInvariant()))
                .WithMessage("Channel must be one of: 'PUSH', 'EMAIL', 'SMS', 'IN_APP'.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.SearchTerm), () =>
        {
            RuleFor(x => x.SearchTerm!)
                .MaximumLength(100)
                .WithMessage("SearchTerm cannot exceed 100 characters.");
        });
    }
}
