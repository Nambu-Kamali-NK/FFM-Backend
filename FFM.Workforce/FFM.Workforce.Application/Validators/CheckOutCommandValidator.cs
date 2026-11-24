using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators
{
    public class CheckOutCommandValidator : AbstractValidator<CheckOutCommand>
    {
        public CheckOutCommandValidator()
        {
            RuleFor(x => x.ActorUserId)
                .GreaterThan(0).WithMessage("Invalid User ID.");

            When(x => !string.IsNullOrWhiteSpace(x.CheckOutNotes), () =>
            {
                RuleFor(x => x.CheckOutNotes)
                    .MaximumLength(500).WithMessage("CheckOutNotes must not exceed 500 characters.");
            });
        }
    }
}
