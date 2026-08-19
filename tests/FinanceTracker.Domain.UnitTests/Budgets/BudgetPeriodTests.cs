using FinanceTracker.Domain.Budgets;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Budgets
{
    public class BudgetPeriodTests
    {
        [Fact]
        public void Create_WithValidYearAndMonth_Succeeds()
        {
            var period = BudgetPeriod.Create(2026, 8);

            period.Year.Should().Be(2026);
            period.Month.Should().Be(8);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(-1)]
        public void Create_WithInvalidMonth_Throws(int month)
        {
            var act = () => BudgetPeriod.Create(2026, month);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Create_WithNonPositiveYear_Throws()
        {
            var act = () => BudgetPeriod.Create(0, 1);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TwoPeriods_WithSameYearAndMonth_AreEqual()
        {
            BudgetPeriod.Create(2026, 8).Should().Be(BudgetPeriod.Create(2026, 8));
        }

        [Fact]
        public void Contains_DateInSameYearAndMonth_ReturnsTrue()
        {
            var period = BudgetPeriod.Create(2026, 8);

            period.Contains(new DateOnly(2026, 8, 19)).Should().BeTrue();
        }

        [Fact]
        public void Contains_DateInDifferentMonth_ReturnsFalse()
        {
            var period = BudgetPeriod.Create(2026, 8);

            period.Contains(new DateOnly(2026, 9, 1)).Should().BeFalse();
        }
    }
}
