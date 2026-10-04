namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    /// <summary>
    /// Result of GetSpendingInsightsQuery. Trends is always computed data;
    /// only Narrative degrades when the AI call fails, and
    /// NarrativeGeneratedByAi tells the caller which case happened.
    /// </summary>
    public sealed record SpendingInsightsDto(
        IReadOnlyList<CategoryTrendDto> Trends, string Narrative, bool NarrativeGeneratedByAi);
}
