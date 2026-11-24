using System.Linq;
using FFM.Workforce.Application.Commands;
using FluentValidation;

namespace FFM.Workforce.Application.Validators
{
    public class StartBreakCommandValidator : AbstractValidator<StartBreakCommand>
    {
        private static readonly string[] ValidBreakTypes = { "LUNCH", "REST", "PERSONAL" };

        public StartBreakCommandValidator()
        {
            RuleFor(x => x.ActorUserId)
                .GreaterThan(0)
                .WithMessage("ActorUserId must be valid.");

            RuleFor(x => x.BreakType)
                .NotEmpty()
                .WithMessage("BreakType is required.")
                .Must(b => b != null && ValidBreakTypes.Contains(b.ToUpperInvariant()))
                .WithMessage("BreakType must be 'LUNCH', 'REST', or 'PERSONAL'.");
        }
    }
}
