namespace FinanceTracker.Application.Transactions.GetSpendingInsights
{
    /// <summary>
    /// Result of GetSpendingInsightsQuery. Trends is always the computed,
    /// trustworthy data regardless of whether the AI call succeeded --
    /// Narrative is the only field that degrades on an IInsightsGenerator
    /// failure. NarrativeGeneratedByAi tells the caller which case happened,
    /// per docs/adr/0010-ai-insights-provider-and-integration-design.md.
    /// </summary>
    public sealed record SpendingInsightsDto(
        IReadOnlyList<CategoryTrendDto> Trends, string Narrative, bool NarrativeGeneratedByAi);
}
