using System;
using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators;

public class AssignRosterCommandValidator : AbstractValidator<AssignRosterCommand>
{
    public AssignRosterCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("EmployeeId must be greater than 0.");

        RuleFor(x => x.ShiftId)
            .GreaterThan(0).WithMessage("ShiftId must be greater than 0.");

        RuleFor(x => x.RosterDate)
            .NotEmpty().WithMessage("RosterDate is required.")
            .Must(d => d.Date > DateTime.UtcNow.Date)
            .WithMessage("Shift assignment or update is strictly restricted to upcoming days (tomorrow onwards). Present date (today) and past shifts cannot be modified.");

        RuleFor(x => x.Zone)
            .MaximumLength(100).WithMessage("Zone cannot exceed 100 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.");
    }
}
