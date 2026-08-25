using FinanceTracker.Application.Budgets.CreateBudget;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Budgets.CreateBudget
{
    public class CreateBudgetCommandValidatorTests
    {
        private readonly CreateBudgetCommandValidator _validator = new();

        private static CreateBudgetCommand ValidCommand() =>
            new(CategoryId.New(), 2026, 8, 500m, "USD");

        [Fact]
        public void Validate_WithValidCommand_IsValid()
        {
            _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultCategoryId_IsInvalid()
        {
            var result = _validator.Validate(ValidCommand() with { CategoryId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBudgetCommand.CategoryId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(-1)]
        public void Validate_WithInvalidMonth_IsInvalid(int month)
        {
            var result = _validator.Validate(ValidCommand() with { Month = month });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBudgetCommand.Month));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WithNonPositiveYear_IsInvalid(int year)
        {
            var result = _validator.Validate(ValidCommand() with { Year = year });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBudgetCommand.Year));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void Validate_WithNonPositiveLimitAmount_IsInvalid(decimal limitAmount)
        {
            var result = _validator.Validate(ValidCommand() with { LimitAmount = limitAmount });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBudgetCommand.LimitAmount));
        }

        [Theory]
        [InlineData("")]
        [InlineData("us")]
        [InlineData("usd")]
        [InlineData("USDD")]
        public void Validate_WithInvalidCurrency_IsInvalid(string currency)
        {
            var result = _validator.Validate(ValidCommand() with { Currency = currency });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBudgetCommand.Currency));
        }
    }
}
