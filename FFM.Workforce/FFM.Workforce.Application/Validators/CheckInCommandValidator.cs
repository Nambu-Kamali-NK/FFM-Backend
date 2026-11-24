using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators
{
    public class CheckInCommandValidator : AbstractValidator<CheckInCommand>
    {
        public CheckInCommandValidator()
        {
            RuleFor(x => x.ActorUserId)
                .GreaterThan(0).WithMessage("Invalid User ID.");

            When(x => !string.IsNullOrWhiteSpace(x.CheckInNotes), () =>
            {
                RuleFor(x => x.CheckInNotes)
                    .MaximumLength(500).WithMessage("CheckInNotes must not exceed 500 characters.");
            });
        }
    }
}
