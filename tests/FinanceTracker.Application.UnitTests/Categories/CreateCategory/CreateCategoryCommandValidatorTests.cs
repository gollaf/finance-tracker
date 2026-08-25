using FinanceTracker.Application.Categories.CreateCategory;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Categories.CreateCategory
{
    public class CreateCategoryCommandValidatorTests
    {
        private readonly CreateCategoryCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            var command = new CreateCategoryCommand("Groceries");

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithMissingName_IsInvalid(string? name)
        {
            var command = new CreateCategoryCommand(name!);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCategoryCommand.Name));
        }

        [Fact]
        public void Validate_WithNullParentCategoryId_IsValid()
        {
            var command = new CreateCategoryCommand("Groceries", null);

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultParentCategoryId_IsInvalid()
        {
            var command = new CreateCategoryCommand("Groceries", default(CategoryId));

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCategoryCommand.ParentCategoryId));
        }
    }
}
