using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class CategoryIdTests
    {
        [Fact]
        public void New_GeneratesUniqueValues()
        {
            CategoryId.New().Should().NotBe(CategoryId.New());
        }

        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new CategoryId(guid).Should().Be(new CategoryId(guid));
        }
    }
}
