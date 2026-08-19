using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Categorization
{
    /// <summary>
    /// Domain service: matches a Transaction description against an ordered
    /// set of CategorizationRules. Stateless because it operates across many
    /// CategorizationRule instances rather than belonging to one aggregate.
    /// Rule-based matching only — there is no AI-assisted fallback for
    /// unmatched transactions in this codebase.
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
