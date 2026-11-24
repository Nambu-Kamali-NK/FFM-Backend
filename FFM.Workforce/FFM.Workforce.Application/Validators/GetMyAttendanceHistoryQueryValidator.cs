using FluentValidation;
using FFM.Workforce.Application.Queries;

namespace FFM.Workforce.Application.Validators
{
    public class GetMyAttendanceHistoryQueryValidator : AbstractValidator<GetMyAttendanceHistoryQuery>
    {
        public GetMyAttendanceHistoryQueryValidator()
        {
            RuleFor(x => x.ActorUserId)
                .GreaterThan(0)
                .WithMessage("ActorUserId must be valid.");

            RuleFor(x => x.Page)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Page number must be at least 1.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("Page size must be between 1 and 100.");

            RuleFor(x => x)
                .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.StartDate.Value <= x.EndDate.Value)
                .WithMessage("StartDate cannot be later than EndDate.")
                .WithName("StartDate");
        }
    }
}
