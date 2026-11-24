using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators
{
    public class EndBreakCommandValidator : AbstractValidator<EndBreakCommand>
    {
        public EndBreakCommandValidator()
        {
            RuleFor(x => x.ActorUserId)
                .GreaterThan(0)
                .WithMessage("ActorUserId must be valid.");

            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage("Notes cannot exceed 500 characters.");
        }
    }
}
