using FluentValidation;
using FFM.Workforce.Application.Commands;

namespace FFM.Workforce.Application.Validators
{
    /// <summary>
    /// Validator for EditEmployeeCommand.
    /// Enforces input sanitization, boundaries, and self-referential manager constraint.
    /// </summary>
    public class EditEmployeeCommandValidator : AbstractValidator<EditEmployeeCommand>
    {
        public EditEmployeeCommandValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("Employee ID must be greater than 0.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.")
                .Matches(@"^[\p{L}\s\.\'\-]+$").WithMessage("Name contains invalid characters.")
                .Must(name => string.IsNullOrEmpty(name) || (!name.Contains("--") && !name.Contains("/*") && !name.Contains("*/") && !name.Contains("''")))
                .WithMessage("Name contains invalid characters.");

            RuleFor(x => x.RoleId)
                .GreaterThan(0).WithMessage("RoleId is required.");

            When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            {
                RuleFor(x => x.Email)
                    .EmailAddress().WithMessage("Email must be a valid email address.")
                    .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Email must be a valid email address.")
                    .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");
            });

            When(x => !string.IsNullOrWhiteSpace(x.Mobile), () =>
            {
                RuleFor(x => x.Mobile)
                    .Matches(@"^\+?[0-9\s\-]{10,20}$").WithMessage("Mobile must contain 10‑15 digits (optional leading +).");
            });

            When(x => x.ManagerId.HasValue, () =>
            {
                RuleFor(x => x.ManagerId)
                    .GreaterThan(0).WithMessage("Manager ID must be greater than 0.")
                    .Must((cmd, mgrId) => mgrId != cmd.Id).WithMessage("Employee cannot be assigned as their own manager.");
            });
        }
    }
}
