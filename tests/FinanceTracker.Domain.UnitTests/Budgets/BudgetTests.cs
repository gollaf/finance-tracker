using FinanceTracker.Domain.Budgets;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Budgets
{
    public class BudgetTests
    {
        [Fact]
        public void Create_WithValidData_Succeeds()
        {
            var categoryId = CategoryId.New();
            var period = BudgetPeriod.Create(2026, 8);
            var limit = Money.Create(400m, "USD");

            var budget = Budget.Create(categoryId, period, limit);

            budget.CategoryId.Should().Be(categoryId);
            budget.Period.Should().Be(period);
            budget.LimitAmount.Should().Be(limit);
        }

        [Fact]
        public void Create_WithZeroLimit_Throws()
        {
            var act = () => Budget.Create(CategoryId.New(), BudgetPeriod.Create(2026, 8), Money.Create(0m, "USD"));

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithNegativeLimit_Throws()
        {
            var act = () => Budget.Create(CategoryId.New(), BudgetPeriod.Create(2026, 8), Money.Create(-10m, "USD"));

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateLimit_WithValidAmount_UpdatesLimit()
        {
            var budget = Budget.Create(CategoryId.New(), BudgetPeriod.Create(2026, 8), Money.Create(400m, "USD"));

            budget.UpdateLimit(Money.Create(500m, "USD"));

            budget.LimitAmount.Should().Be(Money.Create(500m, "USD"));
        }

        [Fact]
        public void UpdateLimit_WithZero_Throws()
        {
            var budget = Budget.Create(CategoryId.New(), BudgetPeriod.Create(2026, 8), Money.Create(400m, "USD"));

            var act = () => budget.UpdateLimit(Money.Create(0m, "USD"));

            act.Should().Throw<ArgumentException>();
        }
    }
}
