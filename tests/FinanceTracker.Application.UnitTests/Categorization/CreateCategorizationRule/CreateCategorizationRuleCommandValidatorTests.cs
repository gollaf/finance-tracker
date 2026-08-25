using FinanceTracker.Application.Categorization.CreateCategorizationRule;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Categorization.CreateCategorizationRule
{
    public class CreateCategorizationRuleCommandValidatorTests
    {
        private readonly CreateCategorizationRuleCommandValidator _validator = new();

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            var command = new CreateCategorizationRuleCommand("walmart", CategoryId.New(), 1);

            _validator.Validate(command).IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithMissingPattern_IsInvalid(string? pattern)
        {
            var command = new CreateCategorizationRuleCommand(pattern!, CategoryId.New(), 1);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCategorizationRuleCommand.Pattern));
        }

        [Fact]
        public void Validate_WithDefaultCategoryId_IsInvalid()
        {
            var command = new CreateCategorizationRuleCommand("walmart", default, 1);

            var result = _validator.Validate(command);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCategorizationRuleCommand.CategoryId));
        }

        [Fact]
        public void Validate_WithNegativePriority_IsValid()
        {
            // No range check on Priority — CategorizationRule.Create doesn't
            // validate it either, so any int is acceptable input here.
            var command = new CreateCategorizationRuleCommand("walmart", CategoryId.New(), -1);

            _validator.Validate(command).IsValid.Should().BeTrue();
        }
    }
}
