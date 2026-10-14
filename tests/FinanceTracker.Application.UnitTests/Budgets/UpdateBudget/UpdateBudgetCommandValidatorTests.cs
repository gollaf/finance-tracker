using FinanceTracker.Application.Budgets.UpdateBudget;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Budgets.UpdateBudget
{
    public class UpdateBudgetCommandValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly UpdateBudgetCommandValidator _validator = new();

        private static UpdateBudgetCommand ValidCommand() => new(Owner, BudgetId.New(), 500m);

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateBudgetCommand.OwnerId));
        }

        [Fact]
        public void Validate_WithDefaultBudgetId_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { BudgetId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateBudgetCommand.BudgetId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WithNonPositiveLimitAmount_IsInvalid(decimal limitAmount)
        {
            var result = _validator.Validate(ValidCommand() with { LimitAmount = limitAmount });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateBudgetCommand.LimitAmount));
        }
    }
}
