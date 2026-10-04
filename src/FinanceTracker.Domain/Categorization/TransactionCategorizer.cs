using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Categorization
{
    /// <summary>
    /// Matches a Transaction description against a set of CategorizationRules;
    /// the matching rule with the lowest Priority wins.
    /// </summary>
    public static class TransactionCategorizer
    {
        public static CategoryId? Categorize(string description, IEnumerable<CategorizationRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            return rules
                .Where(rule => rule.Matches(description))
                .OrderBy(rule => rule.Priority)
                .Select(rule => (CategoryId?)rule.CategoryId)
                .FirstOrDefault();
        }
    }
}
