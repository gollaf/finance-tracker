using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Categorization
{
    public class CategorizationRuleTests
    {
        [Fact]
        public void Create_WithValidPattern_Succeeds()
        {
            var categoryId = CategoryId.New();

            var rule = CategorizationRule.Create("starbucks", categoryId, priority: 1);

            rule.Pattern.Should().Be("starbucks");
            rule.CategoryId.Should().Be(categoryId);
            rule.Priority.Should().Be(1);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithMissingPattern_Throws(string? pattern)
        {
            var act = () => CategorizationRule.Create(pattern!, CategoryId.New(), priority: 1);

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("Starbucks Coffee #4521", true)]
        [InlineData("starbucks", true)]
        [InlineData("STARBUCKS", true)]
        [InlineData("Trader Joe's", false)]
        public void Matches_IsCaseInsensitiveSubstringMatch(string description, bool expected)
        {
            var rule = CategorizationRule.Create("starbucks", CategoryId.New(), priority: 1);

            rule.Matches(description).Should().Be(expected);
        }

        [Fact]
        public void UpdatePattern_WithValidPattern_UpdatesPattern()
        {
            var rule = CategorizationRule.Create("starbucks", CategoryId.New(), priority: 1);

            rule.UpdatePattern("coffee");

            rule.Pattern.Should().Be("coffee");
        }

        [Fact]
        public void UpdatePriority_UpdatesPriority()
        {
            var rule = CategorizationRule.Create("starbucks", CategoryId.New(), priority: 1);

            rule.UpdatePriority(5);

            rule.Priority.Should().Be(5);
        }
    }
}
