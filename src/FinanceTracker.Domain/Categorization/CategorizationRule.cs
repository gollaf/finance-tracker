using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Categorization
{
    /// <summary>
    /// A pattern matched against a Transaction's description to suggest a
    /// Category automatically. Matches against explicit rules only — there is
    /// no AI-assisted fallback for unmatched transactions in this codebase.
    /// </summary>
    public sealed class CategorizationRule
    {
        public CategorizationRuleId Id { get; }

        public string Pattern { get; private set; }

        public CategoryId CategoryId { get; }

        /// <summary>Lower value is matched first when several rules match.</summary>
        public int Priority { get; private set; }

        private CategorizationRule(CategorizationRuleId id, string pattern, CategoryId categoryId, int priority)
        {
            Id = id;
            Pattern = pattern;
            CategoryId = categoryId;
            Priority = priority;
        }

        public static CategorizationRule Create(string pattern, CategoryId categoryId, int priority)
        {
            ValidatePattern(pattern);
            return new CategorizationRule(CategorizationRuleId.New(), pattern.Trim(), categoryId, priority);
        }

        /// <summary>Case-insensitive substring match against a Transaction description.</summary>
        public bool Matches(string description) =>
            !string.IsNullOrEmpty(description) &&
            description.Contains(Pattern, StringComparison.OrdinalIgnoreCase);

        public void UpdatePattern(string pattern)
        {
            ValidatePattern(pattern);
            Pattern = pattern.Trim();
        }

        public void UpdatePriority(int priority) => Priority = priority;

        private static void ValidatePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Rule pattern is required.", nameof(pattern));
        }
    }
}
