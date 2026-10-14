using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class UserIdTests
    {
        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new UserId(guid).Should().Be(new UserId(guid));
        }

        [Fact]
        public void Default_IsTheEmptyGuid()
        {
            default(UserId).Value.Should().Be(Guid.Empty);
        }
    }
}
