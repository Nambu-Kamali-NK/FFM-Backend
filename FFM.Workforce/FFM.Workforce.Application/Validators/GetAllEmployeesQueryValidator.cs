using System;
using System.Linq;
using FluentValidation;
using FFM.Workforce.Application.Queries;

namespace FFM.Workforce.Application.Validators
{
    public class GetAllEmployeesQueryValidator : AbstractValidator<GetAllEmployeesQuery>
    {
        private static readonly string[] AllowedSortByFields = new[]
        {
            "employeecode",
            "employee_code",
            "name",
            "status"
        };

        private static readonly string[] AllowedSortOrders = new[]
        {
            "asc",
            "desc"
        };

        public GetAllEmployeesQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PageNumber must be greater than or equal to 1.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100.");

            RuleFor(x => x.SortBy)
                .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || AllowedSortByFields.Contains(sortBy.Trim().ToLowerInvariant()))
                .WithMessage("SortBy must be one of: 'employeecode', 'name', 'status'.");

            RuleFor(x => x.SortOrder)
                .Must(sortOrder => string.IsNullOrWhiteSpace(sortOrder) || AllowedSortOrders.Contains(sortOrder.Trim().ToLowerInvariant()))
                .WithMessage("SortOrder must be either 'asc' or 'desc'.");
        }
    }
}
