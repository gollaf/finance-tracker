using FinanceTracker.Domain.Categorization;
using FinanceTracker.Domain.Common;
using FluentAssertions;

namespace FinanceTracker.Domain.UnitTests.Categorization
{
    public class TransactionCategorizerTests
    {
        private static readonly UserId Owner = new(Guid.NewGuid());

        [Fact]
        public void Categorize_WithMatchingRule_ReturnsItsCategoryId()
        {
            var coffeeCategoryId = CategoryId.New();
            var rules = new[] { CategorizationRule.Create(Owner, "starbucks", coffeeCategoryId, priority: 1) };

            var result = TransactionCategorizer.Categorize("Starbucks Coffee #4521", rules);

            result.Should().Be(coffeeCategoryId);
        }

        [Fact]
        public void Categorize_WithNoMatchingRule_ReturnsNull()
        {
            var rules = new[] { CategorizationRule.Create(Owner, "starbucks", CategoryId.New(), priority: 1) };

            var result = TransactionCategorizer.Categorize("Trader Joe's", rules);

            result.Should().BeNull();
        }

        [Fact]
        public void Categorize_WithMultipleMatches_ReturnsLowestPriorityMatch()
        {
            var lowPriorityCategoryId = CategoryId.New();
            var highPriorityCategoryId = CategoryId.New();
            var rules = new[]
            {
                CategorizationRule.Create(Owner, "coffee", lowPriorityCategoryId, priority: 5),
                CategorizationRule.Create(Owner, "starbucks", highPriorityCategoryId, priority: 1),
            };

            var result = TransactionCategorizer.Categorize("Starbucks Coffee #4521", rules);

            result.Should().Be(highPriorityCategoryId);
        }

        [Fact]
        public void Categorize_WithNoRules_ReturnsNull()
        {
            var result = TransactionCategorizer.Categorize(
                "Starbucks Coffee #4521", Array.Empty<CategorizationRule>());

            result.Should().BeNull();
        }
    }
}
