using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Common
{
    public class ImportJobIdTests
    {
        [Fact]
        public void New_GeneratesUniqueValues()
        {
            ImportJobId.New().Should().NotBe(ImportJobId.New());
        }

        [Fact]
        public void TwoIds_WithSameGuid_AreEqual()
        {
            var guid = Guid.NewGuid();

            new ImportJobId(guid).Should().Be(new ImportJobId(guid));
        }
    }
}
