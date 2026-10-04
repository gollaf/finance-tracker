namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Trends is always computed data; only Narrative degrades if the AI call
    /// failed, and NarrativeGeneratedByAi says which happened.
    /// </summary>
    public sealed record SpendingInsightsResponse(
        IReadOnlyList<CategoryTrendResponse> Trends, string Narrative, bool NarrativeGeneratedByAi);
}
