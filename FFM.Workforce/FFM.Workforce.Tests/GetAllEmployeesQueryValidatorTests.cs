using FluentValidation.TestHelper;
using FFM.Workforce.Application.Queries;
using FFM.Workforce.Application.Validators;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class GetAllEmployeesQueryValidatorTests
    {
        private readonly GetAllEmployeesQueryValidator _validator = new();

        [Fact]
        public void Validate_ValidQuery_PassesValidation()
        {
            var query = new GetAllEmployeesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SortBy = "name",
                SortOrder = "asc"
            };

            var result = _validator.TestValidate(query);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("employeecode")]
        [InlineData("employee_code")]
        [InlineData("name")]
        [InlineData("status")]
        public void Validate_AllowedSortByFields_PassesValidation(string sortBy)
        {
            var query = new GetAllEmployeesQuery { SortBy = sortBy };
            var result = _validator.TestValidate(query);
            result.ShouldNotHaveValidationErrorFor(q => q.SortBy);
        }

        [Theory]
        [InlineData("id")]
        [InlineData("createdat")]
        [InlineData("invalid_field")]
        public void Validate_InvalidSortBy_FailsValidation(string invalidSortBy)
        {
            var query = new GetAllEmployeesQuery { SortBy = invalidSortBy };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor(q => q.SortBy)
                  .WithErrorMessage("SortBy must be one of: 'employeecode', 'name', 'status'.");
        }

        [Theory]
        [InlineData("asc")]
        [InlineData("desc")]
        public void Validate_AllowedSortOrders_PassesValidation(string sortOrder)
        {
            var query = new GetAllEmployeesQuery { SortOrder = sortOrder };
            var result = _validator.TestValidate(query);
            result.ShouldNotHaveValidationErrorFor(q => q.SortOrder);
        }

        [Fact]
        public void Validate_InvalidSortOrder_FailsValidation()
        {
            var query = new GetAllEmployeesQuery { SortOrder = "invalid_order" };
            var result = _validator.TestValidate(query);
            result.ShouldHaveValidationErrorFor(q => q.SortOrder)
                  .WithErrorMessage("SortOrder must be either 'asc' or 'desc'.");
        }
    }
}
