using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class BudgetIdTests
    {
        [Fact]
        public void New_GeneratesUniqueValues()
        {
            BudgetId.New().Should().NotBe(BudgetId.New());
        }

        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new BudgetId(guid).Should().Be(new BudgetId(guid));
        }
    }
}
