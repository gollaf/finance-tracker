using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class CategorizationRuleIdTests
    {
        [Fact]
        public void New_GeneratesUniqueValues()
        {
            CategorizationRuleId.New().Should().NotBe(CategorizationRuleId.New());
        }

        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new CategorizationRuleId(guid).Should().Be(new CategorizationRuleId(guid));
        }
    }
}
