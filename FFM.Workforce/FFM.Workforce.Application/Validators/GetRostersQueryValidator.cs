using System;
using FluentValidation;
using FFM.Workforce.Application.Queries;

namespace FFM.Workforce.Application.Validators;

public class GetRostersQueryValidator : AbstractValidator<GetRostersQuery>
{
    public GetRostersQueryValidator()
    {
        RuleFor(x => x)
            .Must(q => !(q.From.HasValue && q.To.HasValue) || q.To.Value.Date >= q.From.Value.Date)
            .WithMessage("'to' date must be greater than or equal to 'from' date.");

        RuleFor(x => x)
            .Must(q => !(q.From.HasValue && q.To.HasValue) || (q.To.Value.Date - q.From.Value.Date).TotalDays <= 31)
            .WithMessage("Date range cannot exceed 31 days.");

        When(x => x.EmployeeId.HasValue, () =>
        {
            RuleFor(x => x.EmployeeId!.Value)
                .GreaterThan(0).WithMessage("EmployeeId must be greater than 0.");
        });

        When(x => !string.IsNullOrEmpty(x.Zone), () =>
        {
            RuleFor(x => x.Zone)
                .MaximumLength(100).WithMessage("Zone cannot exceed 100 characters.");
        });
    }
}
