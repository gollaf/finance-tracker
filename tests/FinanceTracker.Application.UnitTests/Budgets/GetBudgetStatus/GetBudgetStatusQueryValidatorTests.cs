using FinanceTracker.Application.Budgets.GetBudgetStatus;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Application.UnitTests.Budgets.GetBudgetStatus
{
    public class GetBudgetStatusQueryValidatorTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());
        private readonly GetBudgetStatusQueryValidator _validator = new();

        [Fact]
        public void Validate_WithValidQuery_IsValid()
        {
            var query = new GetBudgetStatusQuery(Owner, BudgetId.New());

            _validator.Validate(query).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithDefaultOwnerId_IsInvalid()
        {
            var result = _validator.Validate(new GetBudgetStatusQuery(Owner, BudgetId.New()) with { OwnerId = default });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetBudgetStatusQuery.OwnerId));
        }

        [Fact]
        public void Validate_WithDefaultBudgetId_IsInvalid()
        {
            var query = new GetBudgetStatusQuery(Owner, default);

            var result = _validator.Validate(query);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == nameof(GetBudgetStatusQuery.BudgetId));
        }
    }
}
