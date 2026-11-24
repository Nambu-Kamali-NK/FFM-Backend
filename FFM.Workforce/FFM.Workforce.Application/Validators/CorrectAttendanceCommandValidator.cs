using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators
{
    public class CorrectAttendanceCommandValidator : AbstractValidator<CorrectAttendanceCommand>
    {
        public CorrectAttendanceCommandValidator()
        {
            RuleFor(x => x.AttendanceId)
                .GreaterThan(0)
                .WithMessage("Valid Attendance ID is required.");

            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithMessage("Correction reason is required.")
                .MaximumLength(500)
                .WithMessage("Reason cannot exceed 500 characters.");

            RuleFor(x => x)
                .Must(x => !x.CheckOutTime.HasValue || !x.CheckInTime.HasValue || x.CheckOutTime.Value >= x.CheckInTime.Value)
                .WithMessage("Check-out time must be after or equal to check-in time.");
        }
    }
}
