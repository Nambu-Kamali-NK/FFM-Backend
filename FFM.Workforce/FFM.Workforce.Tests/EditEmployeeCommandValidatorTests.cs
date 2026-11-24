#nullable enable
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace FFM.Workforce.Tests
{
    public class EditEmployeeCommandValidatorTests
    {
        private readonly EditEmployeeCommandValidator _validator = new();

        [Fact]
        public void Validate_ValidCommand_Passes()
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = "Jane Doe",
                Email = "jane.doe@example.com",
                Mobile = "9876543210",
                RoleId = 6,
                ManagerId = 5
            };

            var result = _validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_InvalidId_Fails(int id)
        {
            var command = new EditEmployeeCommand
            {
                Id = id,
                Name = "Jane Doe",
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Validate_EmptyName_Fails(string? name)
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = name!,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Validate_NameExceedsMaxLength_Fails()
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = new string('A', 101),
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("DROP TABLE Employees;")]
        [InlineData("Name123")]
        [InlineData("Name#@!")]
        public void Validate_NameInvalidCharacters_Fails(string invalidName)
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = invalidName,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData("invalid-email")]
        [InlineData("user@")]
        [InlineData("@domain.com")]
        [InlineData("user@domain")]
        public void Validate_MalformedEmail_Fails(string invalidEmail)
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = "Jane Doe",
                Email = invalidEmail,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("abc1234567")]
        [InlineData("1234567890123456")] // 16 digits > max 15
        public void Validate_InvalidMobile_Fails(string invalidMobile)
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = "Jane Doe",
                Mobile = invalidMobile,
                RoleId = 6
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Mobile);
        }

        [Fact]
        public void Validate_SelfManager_Fails()
        {
            var command = new EditEmployeeCommand
            {
                Id = 16,
                Name = "Jane Doe",
                RoleId = 6,
                ManagerId = 16 // Self-manager!
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.ManagerId);
        }
    }
}
