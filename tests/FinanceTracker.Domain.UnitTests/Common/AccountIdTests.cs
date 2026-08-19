using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class AccountIdTests
    {
        [Fact]
        public void New_GeneratesUniqueValues()
        {
            AccountId.New().Should().NotBe(AccountId.New());
        }

        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new AccountId(guid).Should().Be(new AccountId(guid));
        }
    }
}
