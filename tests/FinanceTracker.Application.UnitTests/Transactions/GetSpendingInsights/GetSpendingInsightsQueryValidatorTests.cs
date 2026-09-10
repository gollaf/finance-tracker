using FinanceTracker.Application.Transactions.GetSpendingInsights;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Transactions.GetSpendingInsights
{
    public class GetSpendingInsightsQueryValidatorTests
    {
        private readonly GetSpendingInsightsQueryValidator _validator = new();

        private static GetSpendingInsightsQuery ValidQuery() => new(AccountId.New(), 2026, 6);

        [Fact]
        public void Validate_WithValidQuery_IsValid()
        {
            _validator.Validate(ValidQuery()).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultAccountId_IsInvalid()
        {
            var result = _validator.Validate(ValidQuery() with { AccountId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetSpendingInsightsQuery.AccountId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(-1)]
        public void Validate_WithInvalidMonth_IsInvalid(int month)
        {
            var result = _validator.Validate(ValidQuery() with { Month = month });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetSpendingInsightsQuery.Month));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WithNonPositiveYear_IsInvalid(int year)
        {
            var result = _validator.Validate(ValidQuery() with { Year = year });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetSpendingInsightsQuery.Year));
        }
    }
}
