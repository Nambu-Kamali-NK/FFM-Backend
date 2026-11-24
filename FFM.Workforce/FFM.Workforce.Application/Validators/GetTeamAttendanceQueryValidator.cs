using FluentValidation;
using FFM.Workforce.Application.Queries;

namespace FFM.Workforce.Application.Validators
{
    public class GetTeamAttendanceQueryValidator : AbstractValidator<GetTeamAttendanceQuery>
    {
        public GetTeamAttendanceQueryValidator()
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
        }
    }
}
