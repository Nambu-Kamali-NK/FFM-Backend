using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators
{
    public class SubmitLeaveRequestCommandValidator : AbstractValidator<SubmitLeaveRequestCommand>
    {
        public SubmitLeaveRequestCommandValidator()
        {
            RuleFor(x => x.LeaveTypeId)
                .GreaterThan(0)
                .WithMessage("A valid LeaveTypeId is required.");

            RuleFor(x => x.StartDate)
                .NotEmpty()
                .WithMessage("StartDate is required.");

            RuleFor(x => x.EndDate)
                .NotEmpty()
                .WithMessage("EndDate is required.")
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("EndDate must be greater than or equal to StartDate.");

            RuleFor(x => x)
                .Must(x => !x.IsHalfDay || x.StartDate.Date == x.EndDate.Date)
                .WithMessage("Half-day leave requests must have identical StartDate and EndDate.");

            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Reason is required.")
                .MaximumLength(500)
                .WithMessage("Reason cannot exceed 500 characters.");
        }
    }
}
