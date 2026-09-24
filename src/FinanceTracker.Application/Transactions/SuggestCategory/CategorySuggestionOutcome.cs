namespace FinanceTracker.Application.Transactions.SuggestCategory
{
    /// <summary>What SuggestCategoryForTransactionCommand ended up doing, and why.</summary>
    public enum CategorySuggestionOutcome
    {
        /// <summary>The AI picked a Category and it was saved.</summary>
        Categorized,

        /// <summary>
        /// The Transaction already had a Category -- assigned by a rule, by
        /// the user, or by an earlier delivery of this same message. Nothing
        /// changed; this is what makes handling a duplicate message safe.
        /// </summary>
        AlreadyCategorized,

        /// <summary>The Transaction no longer exists (deleted before this ran).</summary>
        TransactionNotFound,

        /// <summary>There are no Categories at all, so the AI wasn't asked.</summary>
        NoCategoriesDefined,

        /// <summary>The AI answered that none of the Categories fits.</summary>
        NoSuitableCategory,

        /// <summary>
        /// No usable AI answer (not configured, unreachable, timed out, or
        /// an invalid reply). The Transaction is left uncategorized.
        /// </summary>
        SuggestionUnavailable,
    }
}
