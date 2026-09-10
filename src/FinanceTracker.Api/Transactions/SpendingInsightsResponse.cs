namespace FinanceTracker.Api.Transactions
{
    /// <summary>
    /// Trends is always the computed, trustworthy data; Narrative is the
    /// only field that degrades if the AI call failed --
    /// NarrativeGeneratedByAi tells the caller which happened. See
    /// docs/adr/0010-ai-insights-provider-and-integration-design.md.
    /// </summary>
    public sealed record SpendingInsightsResponse(
        IReadOnlyList<CategoryTrendResponse> Trends, string Narrative, bool NarrativeGeneratedByAi);
}
