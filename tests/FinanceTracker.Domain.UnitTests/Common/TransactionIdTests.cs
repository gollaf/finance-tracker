using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class TransactionIdTests
    {
        [Fact]
        public void New_GeneratesUniqueValues()
        {
            TransactionId.New().Should().NotBe(TransactionId.New());
        }

        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new TransactionId(guid).Should().Be(new TransactionId(guid));
        }
    }
}
